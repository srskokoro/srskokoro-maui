﻿namespace Kokoro.Internal;
using Blake2Fast;
using Blake2Fast.Implementation;
using Kokoro.Common.Util;
using Kokoro.Internal.Sqlite;
using Microsoft.Data.Sqlite;
using System.Runtime.InteropServices;

partial class FieldedEntity {
	internal const int MaxClassCount = byte.MaxValue;

	[SuppressMessage("Style", "IDE1006:Naming Styles")]
	private static class SchemaRewrite {

		internal static class DataWhenNoSharedFields {
			public static readonly byte[] ReadOnlyBytes;

			static DataWhenNoSharedFields() {
				const int length = FieldsDesc.VarIntLengthForEmpty;
				Debug.Assert(length == VarInts.LengthForZero);
				Debug.Assert(length == 1);

				var buffer = new byte[length] { 0 };
				Debug.Assert(buffer.SequenceEqual(VarInts.Bytes(FieldsDesc.Empty)));

				ReadOnlyBytes = buffer;
			}
		}

		internal struct ClassInfo {
			public long rowid;
			public UniqueId uid;
			public byte[] csum;
			public int ord;

			public ClassInfo(
				long rowid, UniqueId uid, byte[] csum, int ord
			) {
				this.rowid = rowid;
				this.uid = uid;
				this.csum = csum;
				this.ord = ord;
			}
		}

		internal struct FieldInfo {
			public long rowid;

			public int cls_ord;
			public FieldStoreType sto;
			public int ord;
			public FieldSpec old_idx_sto;

			public string name;
			public FieldVal? new_fval;
#if DEBUG
			public UniqueId cls_uid;
#endif

			public FieldInfo(
				long rowid,
				int cls_ord, FieldStoreType sto, int ord, FieldSpec old_idx_sto,
				string name, FieldVal? new_fval
#if DEBUG
				, UniqueId cls_uid
#endif
			) {
				this.rowid = rowid;

				this.cls_ord = cls_ord;
				this.sto = sto;
				this.ord = ord;
				this.old_idx_sto = old_idx_sto;

				this.name = name;
				this.new_fval = new_fval;
#if DEBUG
				this.cls_uid = cls_uid;
#endif
			}
		}

		internal sealed class Comparisons {
			private readonly List<FieldInfo> fldList;
			private readonly List<ClassInfo> clsList;

			public Comparisons(List<FieldInfo> fldList, List<ClassInfo> clsList) {
				this.fldList = fldList;
				this.clsList = clsList;
			}

			public int fldList_compare(byte x, byte y) {
				ref var r0 = ref fldList.AsSpan().DangerousGetReference();
				ref var a = ref U.Add(ref r0, x);
				ref var b = ref U.Add(ref r0, y);

				int cmp;
				// Partition the sorted array by field store type
				{
					// NOTE: Using `Enum.CompareTo()` has a boxing cost, which
					// sadly, JIT doesn't optimize out (for now). So we must
					// cast the enums to their int counterparts to avoid the
					// unnecessary box.
					var a_sto = (FieldStoreTypeInt)a.sto;
					var b_sto = (FieldStoreTypeInt)b.sto;
					cmp = a_sto.CompareTo(b_sto);
					if (cmp != 0) goto Return;
				}
				{
					cmp = a.cls_ord.CompareTo(b.cls_ord);
					if (cmp != 0) goto Return;
				}
				{
					cmp = a.ord.CompareTo(b.ord);
					if (cmp != 0) goto Return;
				}
				{
					Debug.Assert(a.rowid != b.rowid, $"Expecting no " +
						$"duplicates but found a duplicate field entry " +
						$"with name id {a.rowid}");
				}
				{
					cmp = string.CompareOrdinal(a.name, b.name);
					Debug.Assert(cmp != 0, $"Impossible! Two fields have " +
						$"different name ids ({a.rowid} and {b.rowid}) but " +
						$"same name: {a.name}");
				}
			Return:
				return cmp;
			}

			public int clsList_compare(byte x, byte y) {
				ref var r0 = ref clsList.AsSpan().DangerousGetReference();
				return U.Add(ref r0, x).uid.CompareTo(U.Add(ref r0, y).uid);
			}
		}

		internal static class Comparison_clsList {
			private static Comparison<(long RowId, byte[]? Csum)>? _Inst;

			internal static Comparison<(long RowId, byte[]? Csum)> Inst {
				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				get => _Inst ??= Impl;
			}

			[MethodImpl(MethodImplOptions.AggressiveOptimization)]
			[SkipLocalsInit]
			private static int Impl(
				(long RowId, byte[]? Csum) a,
				(long RowId, byte[]? Csum) b
			) {
				var x = a.Csum;
				if (x == null) goto LT; // This becomes a conditional jump forward to not favor it

				var y = b.Csum;
				if (y == null) goto GT; // This becomes a conditional jump forward to not favor it

				// See also, https://crypto.stackexchange.com/questions/54544/how-to-to-calculate-the-hash-of-an-unordered-set
				return x.AsDangerousROSpan().SequenceCompareTo(y.AsDangerousROSpan());

			LT: return -1;
			GT: return 1;
			}
		}
	}

	/// <remarks>
	/// CONTRACT:
	/// <br/>- Must be called while inside a transaction (ideally, using <see cref="NestingWriteTransaction"/>).
	/// <br/>- Must set <see cref="_SchemaId"/> beforehand to the rowid of the
	/// desired base schema.
	/// <br/>- Must have <paramref name="oldSchemaId"/> with the rowid of the
	/// actual schema being used by the <see cref="FieldedEntity">fielded entity</see>,
	/// as loaded beforehand while inside the transaction.
	/// <br/>- If <see cref="_SchemaId"/> != <paramref name="oldSchemaId"/>,
	/// then <see cref="FieldsReader.OverrideSharedStore(long)"><paramref name="fr"/>.OverrideSharedStore(<paramref name="oldSchemaId"/>)</see>
	/// must be called beforehand, at least once, while inside the transaction.
	/// <para>
	/// Violation of the above contract may result in undefined behavior.
	/// </para>
	/// </remarks>
	/// <returns>
	/// The new schema rowid (never zero).
	/// </returns>
	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	[SkipLocalsInit]
	private protected long RewriteSchema(long oldSchemaId, ref FieldsReader fr, ref FieldsWriter fw, int hotStoreLimit = DefaultHotStoreLimit) {
		DAssert_FieldsWriterPriorRewrite(ref fw);

		var clsSet = _Classes;
		// ^- NOTE: Soon, the class set will contain only the newly added
		// classes (i.e., classes awaiting addition). The code after will make
		// sure that happens. Later, it'll also include direct classes from the
		// base schema, provided that those awaiting removal are filtered out.
		// Afterwards, it'll also include the indirect classes, as included by
		// the classes in the current set. In the end, the resulting set will
		// contain all the classes of the fielded entity under a new schema.

		KokoroSqliteDb db;
		{
			// Reinitialize the class set with only the classes awaiting
			// addition, while also obtaining the class change set
			// --

			HashSet<long> clsChgSet; // The class change set

			if (clsSet != null) {
				clsChgSet = clsSet.Changes!;
				if (clsChgSet == null) {
					// NOTE: The favored case is schema rewrites due to shared
					// field changes, often without any class changes.
					clsSet.Clear();
					goto FallbackForClsChgSet;
				} else {
					// NOTE: The intersection represents the newly added
					// classes. Classes present in the change set but not in the
					// resulting set, represent the classes awaiting removal.
					clsSet.IntersectWith(clsChgSet);
					goto DoneWithClsChgSet;
				}
			} else {
				_Classes = clsSet = new();
			}
		FallbackForClsChgSet:
			clsChgSet = clsSet;
		DoneWithClsChgSet:
			;

			// Get the base schema's direct classes
			// --

			db = fr.Db;
			using var cmd = db.CreateCommand();
			cmd.Set($"SELECT cls FROM {Prot.SchemaToClass} WHERE (ind,schema)=(0,$schema)")
				.AddParams(new("$schema", _SchemaId));

			using var r = cmd.ExecuteReader();
			while (r.Read()) {
				r.DAssert_Name(0, "cls");
				long cls = r.GetInt64(0);

				if (!clsChgSet.Contains(cls))
					clsSet.Add(cls);
			}
		}

		int dclsCount = clsSet.Count; // The number of direct classes
		if (dclsCount > MaxClassCount) goto E_TooManyClasses;

		List<(long RowId, byte[]? Csum)> clsList = new(dclsCount);

		foreach (long rowid in clsSet) {
			clsList.Add((RowId: rowid, Csum: null));
		}

		// --
		{
			SqliteParameter cmd_rowid = new() { ParameterName = "$rowid" };

			using var clsCmd = db.CreateCommand();
			clsCmd.Set($"SELECT csum FROM {Prot.Class} WHERE rowid=$rowid")
				.AddParams(cmd_rowid);

			using var inclCmd = db.CreateCommand();
			inclCmd.Set($"SELECT incl FROM {Prot.ClassToInclude} WHERE cls=$rowid")
				.AddParams(cmd_rowid);

			for (int i = 0; i < clsList.Count; i++) {
				ref var cls = ref clsList.AsSpan().DangerousGetReferenceAt(i);
				cmd_rowid.Value = cls.RowId;

				// Get the needed class info
				using (var r = clsCmd.ExecuteReader()) {
					if (r.Read()) {
						r.DAssert_Name(0, "csum");
						cls.Csum = r.GetBytes(0);
					} else {
						// The user probably attached a nonexistent class to the
						// fielded entity. Ignore it then. We'll skip it later.

						// A null `csum` indicates that it should be skipped.
						Debug.Assert(cls.Csum == null);

						// Remove from set (since the set describes the fielded
						// entity's classes under the new schema).
						clsSet.Remove(cls.RowId);
					}
				}

				// Get the included classes, adding them as indirect classes
				using (var r = inclCmd.ExecuteReader()) {
					while (r.Read()) {
						r.DAssert_Name(0, "incl");
						long incl = r.GetInt64(0);
						if (clsSet.Add(incl)) {
							clsList.Add((RowId: incl, Csum: null));
						}
					}
				}
			}
		}

		// Partition the list of classes into two, direct and indirect classes,
		// then sort each partition into a predictable order.
		{
			var clsListSpan = clsList.AsSpan();
			if (clsListSpan.Length > MaxClassCount) goto E_TooManyClasses;

			// NOTE: Partitioning the list of classes is necessary so that the
			// resulting schema `usum` hash would be unique even if the set of
			// classes hashed is the same for another but differs only in what
			// classes are direct classes.

			if (dclsCount != 0) {
				Debug.Assert(dclsCount > 0);
				var comparison = SchemaRewrite.Comparison_clsList.Inst;
				clsListSpan.Slice(0, dclsCount).Sort(comparison);
				clsListSpan.Slice(dclsCount).Sort(comparison);
				// ^- NOTE: Unless we have at least one direct class, we won't
				// have a nonempty list of indirect classes, since there won't
				// be any direct classes to include the indirect classes.
			}
		}

		long schemaId;
		byte[] schemaUsum;

		// Generate the `usum` for the bare schema
		{
			var hasher = Blake2b.CreateIncrementalHasher(SchemaUsumDigestLength);

			// Length-prepend for the (sub)list of direct classes
			{
				Debug.Assert(dclsCount >= 0);
				hasher.UpdateLE(dclsCount);
			}

			// Hash the `csum`s from the list of classes
			{
				int i = 0;
				int n = clsList.Count;
				if (i >= n) goto Hashed;

				ref var r0 = ref clsList.AsSpan().DangerousGetReference();
				do {
					byte[]? csum = U.Add(ref r0, i).Csum;
					// Skips nonexistent classes (indicated by null `csum`)
					if (csum != null) hasher.Update(csum);
				} while (++i < n);

			Hashed:
				;
			}

			// Bare schemas are schemas without shared data (either there are no
			// shared fields or all shared fields have null field vals).
			schemaUsum = FinishWithSchemaUsum(ref hasher, hasSharedData: false);
		}

		// Resolve the bare schema's rowid
		// --

		using (var cmd = db.CreateCommand()) {
			cmd.Set($"SELECT rowid FROM {Prot.Schema} WHERE usum=$usum")
				.AddParams(new("$usum", schemaUsum));

			using var r = cmd.ExecuteReader();
			if (r.Read()) {
				r.DAssert_Name(0, "rowid");
				schemaId = r.GetInt64(0);
			} else {
				schemaId = 0;
			}
		}

		// NOTE: Must also ensure that the schema rowid is never zero, even if
		// the rowid came from the DB.
		if (schemaId == 0) {
			goto InitBareSchema;
		}
	InitBareSchema_Done:
		Debug.Assert(schemaId != 0);

		// -=-

		Dictionary<long, FieldVal> fldMapOld = new(16);

		// Process the field changes
		{
			Fields? fields = _Fields;
			if (fields == null) goto NoFieldChanges;

			FieldChanges? changes = fields.Changes;
			if (changes == null) goto NoFieldChanges;

			var changes_iter = changes.GetEnumerator();
			if (!changes_iter.MoveNext()) goto NoFieldChanges;

			db.ReloadNameIdCaches(); // Needed by `db.LoadStale…()` below

			SqliteCommand? resCmd = null;
			SqliteParameter resCmd_fld = null!;

			try {
			Loop:
				var (fname, fchg) = changes_iter.Current;

				if (fchg.GetType() == typeof(StringKey)) {
					goto ReadyToResolveFieldVal;
				}

				Debug.Assert(fchg is FieldVal);
				var fval = U.As<FieldVal>(fchg);

			MapFieldVal:
				{
					long fld = db.LoadStaleOrEnsureNameId(fname);

					bool added = fldMapOld.TryAdd(fld, fval);
					Debug.Assert(added, $"Shouldn't happen if running in a " +
						$"`{nameof(NestingWriteTransaction)}` or equivalent");

					goto Continue;
				}

			ReadyToResolveFieldVal:
				if (resCmd != null) {
					goto ResolveFieldVal;
				} else {
					goto InitToResolveFieldVal;
				}

			ResolveFieldVal:
				{
					Debug.Assert(fchg is StringKey);
					var fsrc = U.As<StringKey>(fchg);

					long fld2 = db.LoadStaleNameId(fsrc);
					resCmd_fld.Value = fld2;

					using (var r = resCmd.ExecuteReader()) {
						if (r.Read()) {
							r.DAssert_Name(0, "idx_sto");
							FieldSpec fspec2 = r.GetInt32(0);
							fspec2.DAssert_Valid();

							fval = fr.Read(fspec2);
						} else {
							fval = OnLoadFloatingField(db, fld2) ?? FieldVal.Null;
						}
					}
					goto MapFieldVal;
				}

			Continue:
				if (!changes_iter.MoveNext()) {
					goto Break;
				} else {
					// This becomes a conditional jump backward -- similar to a
					// `do…while` loop.
					goto Loop;
				}

			InitToResolveFieldVal:
				{
					resCmd = db.CreateCommand();
					resCmd.Set(
						$"SELECT idx_sto FROM {Prot.SchemaToField}\n" +
						$"WHERE (schema,fld)=($schema,$fld)"
					).AddParams(
						new("$schema", oldSchemaId),
						resCmd_fld = new() { ParameterName = "$fld" }
					);
					goto ResolveFieldVal;
				}

			Break:
				;

			} finally {
				resCmd?.Dispose();
			}

		NoFieldChanges:
			;
		}

		// Get the old field values via the old schema
		using (var cmd = db.CreateCommand()) {
			cmd.Set($"SELECT fld,idx_sto FROM {Prot.SchemaToField} WHERE schema=$schema")
				.AddParams(new("$schema", oldSchemaId));

			using var r = cmd.ExecuteReader();
			while (r.Read()) {
				r.DAssert_Name(0, "fld");
				long fld = r.GetInt64(0);

				ref var entry = ref CollectionsMarshal.GetValueRefOrAddDefault(
					fldMapOld, key: fld, out _
				);
				if (entry == null) {
					r.DAssert_Name(1, "idx_sto");
					FieldSpec fspec = r.GetInt32(1);
					fspec.DAssert_Valid();

					entry = fr.Read(fspec);
				}
			}
		}

		// -=-

		int xlc, xhc, xsc;

		using (var cmd = db.CreateCommand()) {
			cmd.Set(
				$"SELECT hotCount,coldCount,sharedCount\n" +
				$"FROM {Prot.Schema} WHERE rowid=$rowid"
			).AddParams(new("$rowid", schemaId));

			using var r = cmd.ExecuteReader();
			if (!r.Read()) {
				Debug.Fail("Bare schema should already exist at this point.");
			}

			r.DAssert_Name(0, "hotCount"); // The max hot field count
			xhc = r.GetInt32(0); // The expected max hot count

			r.DAssert_Name(1, "coldCount"); // The max cold field count
			xlc = r.GetInt32(1) + xhc; // The expected max local count

			r.DAssert_Name(2, "sharedCount"); // The max shared field count
			xsc = r.GetInt32(2); // The expected max shared count
		}

		// --
		{
			if ((uint)xhc > (uint)xlc) {
				goto E_InvalidFieldCounts_InvDat;
			}

			int capacity = (int)Math.Max((uint)xsc, (uint)xlc);

			if ((uint)capacity > (uint)MaxFieldCount) {
				goto E_InvalidFieldCounts_InvDat;
			}

			// The checks above should've ensured the following to be true
			Debug.Assert(0 <= xhc && xhc <= xlc && xlc <= MaxFieldCount);
			Debug.Assert(0 <= xsc);

			fw.InitEntries(capacity);
		}

		// -=-

		int nsc; // The new shared field count

		// Generate the `usum` for the actual schema
		{
			Blake2bHashState hasher;

			using (var cmd = db.CreateCommand()) {
				cmd.Set(
					$"SELECT idx,fld FROM {Prot.SchemaToField}\n" +
					$"WHERE schema=$schema AND loc=0\n" +
					$"ORDER BY idx" // Needed only to force usage of DB index
				).AddParams(new("$schema", schemaId));

				using var r = cmd.ExecuteReader();
				if (r.Read()) {
					hasher = Blake2b.CreateIncrementalHasher(SchemaUsumDigestLength);

					DAssert_BareSchemaUsum(schemaUsum);
					hasher.Update(schemaUsum);

					do {
						r.DAssert_Name(0, "idx");
						int i = r.GetInt32(0);

						if ((uint)i >= (uint)xsc) {
							goto E_IndexBeyondSharedFieldCount_InvDat;
						}

						r.DAssert_Name(1, "fld");
						long fld = r.GetInt64(1);

						if (!fldMapOld.Remove(fld, out var fval)) {
							// This becomes a conditional jump forward to not favor it
							goto NotInOldMap;
						}

					GotEntry:
						{
							Debug.Assert((uint)i < (uint)fw._Entries.Length);
							fw._Entries.DangerousGetReferenceAt(i) = fval;

							fval.FeedTo(ref hasher);
							continue;
						}

					NotInOldMap:
						// Field not defined by the old schema + No field change
						{
							fval = OnSupplantFloatingField(db, fld) ?? FieldVal.Null;
							goto GotEntry;
						}

					E_IndexBeyondSharedFieldCount_InvDat:
						E_IndexBeyondSharedFieldCount_InvDat(schemaId, i, xsc: xsc);

					} while (r.Read());

				} else {
					goto SchemaResolved;
				}
			}

			// Trim null shared field values from the end
			// --

			Debug.Assert((uint)xsc <= (uint)fw._Entries.Length);

			try {
				nsc = fw.TrimNullFValsFromEnd(end: xsc);
			} catch (NullReferenceException) when (fw.HasEntryMissing(0, xsc)) {
				goto E_MissingSharedField_InvDat;
			}

			if (nsc != 0) {
				Debug.Assert(nsc > 0);
				// Non-bare schemas are schemas with shared data, having shared
				// fields with at least one not set to a null field value.
				schemaUsum = FinishWithSchemaUsum(ref hasher, hasSharedData: true);
			} else {
				goto SchemaResolved;
			}
		}

		// Resolve the non-bare schema's rowid
		// --

		using (var cmd = db.CreateCommand()) {
			cmd.Set($"SELECT rowid FROM {Prot.Schema} WHERE usum=$usum")
				.AddParams(new("$usum", schemaUsum));

			using var r = cmd.ExecuteReader();
			if (r.Read()) {
				r.DAssert_Name(0, "rowid");
				schemaId = r.GetInt64(0);
			} else {
				schemaId = 0;
			}
		}

		// NOTE: Must also ensure that the schema rowid is never zero, even if
		// the rowid came from the DB.
		if (schemaId == 0) {
			goto InitNonBareSchema;
		}
	InitNonBareSchema_Done:
		Debug.Assert(schemaId != 0);

	SchemaResolved:
		;

		// -=-

		int nextOffset = 0;

		using (var cmd = db.CreateCommand()) {
			cmd.Set(
				$"SELECT idx,fld FROM {Prot.SchemaToField}\n" +
				$"WHERE schema=$schema AND loc=1\n" +
				$"ORDER BY idx" // Needed only to force usage of DB index
			).AddParams(new("$schema", schemaId));

			using var r = cmd.ExecuteReader();
			try {
				while (r.Read()) {
					r.DAssert_Name(0, "idx");
					int i = r.GetInt32(0);

					if ((uint)i >= (uint)xlc) {
						goto E_IndexBeyondLocalFieldCount_InvDat;
					}

					r.DAssert_Name(1, "fld");
					long fld = r.GetInt64(1);

					if (!fldMapOld.Remove(fld, out var fval)) {
						// This becomes a conditional jump forward to not favor it
						goto NotInOldMap;
					}

				GotEntry:
					{
						Debug.Assert((uint)i < (uint)fw._Entries.Length);
						fw._Entries.DangerousGetReferenceAt(i) = fval;

						Debug.Assert((uint)i < (uint)fw._Offsets.Length);
						fw._Offsets.DangerousGetReferenceAt(i) = nextOffset;

						checked {
							nextOffset += (int)fval.CountEncodeLength();
						}

						continue;
					}

				NotInOldMap:
					// Field not defined by the old schema + No field change
					{
						fval = OnSupplantFloatingField(db, fld) ?? FieldVal.Null;
						goto GotEntry;
					}

				E_IndexBeyondLocalFieldCount_InvDat:
					E_IndexBeyondLocalFieldCount_InvDat(schemaId, i, xlc: xlc);
				}
			} catch (OverflowException) {
				goto E_FieldValsLengthTooLarge;
			}
		}

		if ((uint)nextOffset > (uint)MaxFieldValsLength) {
			goto E_FieldValsLengthTooLarge;
		}

		int ldn;

		// Trim null local field values from the end
		{
			Debug.Assert((uint)xlc <= (uint)fw._Entries.Length);

			try {
				ldn = fw.TrimNullFValsFromEnd(end: xlc);
			} catch (NullReferenceException) when (fw.HasEntryMissing(0, xlc)) {
				goto E_MissingLocalField_InvDat;
			}
		}

		// Using `goto` here (instead of `if…else…`) just so to reduce indention
		if (ldn == 0) goto ClearLocalFields;

		Debug.Assert(ldn > 0);
		Debug.Assert(xhc >= 0);

		if ((uint)ldn <= (uint)xhc || nextOffset <= hotStoreLimit) {
			// Case: Hot store only (no real cold store)

			int hotFOffsetSizeM1Or0 = (
				(uint)fw._Offsets.DangerousGetReferenceAt(ldn-1)
			).CountBytesNeededM1Or0();

			FieldsDesc hotFDesc = new(
				fCount: ldn,
				fOffsetSizeM1Or0: hotFOffsetSizeM1Or0
			);

			fw._HotFieldsDesc = hotFDesc;

			// NOTE: The first offset value is never stored, as it'll always be
			// zero otherwise.
			fw._HotStoreLength = VarInts.Length(hotFDesc)
				+ (ldn - 1) * (hotFOffsetSizeM1Or0 + 1)
				+ nextOffset;

			fw._ColdStoreLength = 0;

		} else {
			Debug.Assert(ldn > xhc, $"Needs at least 1 cold field loaded"); // Future-proofing

			// Case: Hot store (with maybe no hot fields) with real cold store
			// (with at least 1 cold field).

			int hotFValsSize = fw._Offsets.DangerousGetReferenceAt(xhc);

			if (xhc != 0) {
				int hotFOffsetSizeM1Or0 = (
					(uint)fw._Offsets.DangerousGetReferenceAt(xhc-1)
				).CountBytesNeededM1Or0();

				FieldsDesc hotFDesc = new(
					fCount: xhc,
					fHasCold: true,
					fOffsetSizeM1Or0: hotFOffsetSizeM1Or0
				);

				fw._HotFieldsDesc = hotFDesc;

				// NOTE: The first offset value is never stored, as it'll always
				// be zero otherwise.
				fw._HotStoreLength = VarInts.Length(hotFDesc)
					+ (xhc - 1) * (hotFOffsetSizeM1Or0 + 1)
					+ hotFValsSize;
			} else {
				// Still need to at least set the "has real cold store" flag in
				// the hot store.
				fw._HotFieldsDesc = FieldsDesc.EmptyWithCold;
				fw._HotStoreLength = FieldsDesc.VarIntLengthForEmptyWithCold;
			}

			int coldFOffsetSizeM1Or0 = (
				(uint)(fw._Offsets.DangerousGetReferenceAt(ldn-1) - hotFValsSize)
			).CountBytesNeededM1Or0();

			int ncc = ldn - xhc;
			FieldsDesc coldFDesc = new(
				fCount: ncc,
				fOffsetSizeM1Or0: coldFOffsetSizeM1Or0
			);

			fw._ColdFieldsDesc = coldFDesc;

			// NOTE: The first offset value is never stored, as it'll always be
			// zero otherwise.
			fw._ColdStoreLength = VarInts.Length(coldFDesc)
				+ (ncc - 1) * (coldFOffsetSizeM1Or0 + 1)
				+ (nextOffset - hotFValsSize);
		}

		// Skip below
		goto DoneWithLocalFields;

	ClearLocalFields:
		{
			fw._ColdStoreLength = fw._HotStoreLength = 0;
			goto DoneWithLocalFields;
		}

	DoneWithLocalFields:
		;

		// The remaining old field mappings will become floating fields -- i.e.,
		// fields not defined by the (new) schema.
		{
			int fldMapOldCount = fldMapOld.Count;
			if (fldMapOldCount != 0) {
				var floFlds = fw._FloatingFields;
				if (floFlds == null) {
					fw._FloatingFields = floFlds = new(fldMapOldCount);
				} else {
					floFlds.EnsureCapacity(fldMapOldCount);
				}
				foreach (var pair in fldMapOld) {
					floFlds.Add((pair.Key, pair.Value));
				}
			}
		}

	// -=-

	Done:
		DAssert_FieldsWriterAfterRewrite(ref fw);
		Debug.Assert(schemaId != 0);
		return schemaId; // ---

	InitBareSchema:
		schemaId = InitBareSchema(clsList, schemaUsum);
		goto InitBareSchema_Done;

	InitNonBareSchema:
		schemaId = InitNonBareSchema(
			bareSchemaId: schemaId,
			nonBareUsum: schemaUsum,
			ref fw, nsc: nsc
		);
		goto InitNonBareSchema_Done;

	E_FieldValsLengthTooLarge:
		E_FieldValsLengthTooLarge((uint)nextOffset);

	E_MissingLocalField_InvDat:
		E_MissingLocalField_InvDat(schemaId);

	E_MissingSharedField_InvDat:
		E_MissingSharedField_InvDat(schemaId);

	E_InvalidFieldCounts_InvDat:
		E_InvalidFieldCounts_InvDat(schemaId,
			xhc: xhc,
			xlc: xlc,
			xsc: xsc
		);

	E_TooManyClasses:
		E_TooManyClasses(clsSet.Count);

		// -=-

		Debug.Fail("This point should be unreachable.");
		schemaId = 0;
		goto Done;
	}

	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	[SkipLocalsInit]
	private static long InitBareSchema(List<(long RowId, byte[]? Csum)> clsList, byte[] usum) {
		DAssert_BareSchemaUsum(usum);

		// TODO Implement
		throw new NotImplementedException("TODO");
	}

	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	[SkipLocalsInit]
	private static long InitNonBareSchema(long bareSchemaId, byte[] nonBareUsum, ref FieldsWriter fw, int nsc) {
		DAssert_NonBareSchemaUsum(nonBareUsum);

		// TODO Implement
		throw new NotImplementedException("TODO");
	}

	private const int SchemaUsumDigestLength = 31; // 248-bit hash

	private static byte[] FinishWithSchemaUsum(ref Blake2bHashState hasher, bool hasSharedData) {
		const int UsumVer = 1; // The version varint
		const int UsumVerLength = 1; // The varint length is a single byte for now
		Debug.Assert(VarInts.Length(UsumVer) == UsumVerLength);
		Debug.Assert(VarInts.Bytes(UsumVer)[0] == UsumVer);

		Span<byte> usum = stackalloc byte[UsumVerLength + SchemaUsumDigestLength];
		usum[0] = UsumVer; // Prepend version varint
		usum[1] = (byte)((usum[1] & unchecked((sbyte)0xFE)) | hasSharedData.ToByte());

		hasher.Finish(usum[UsumVerLength..]);

		// TODO In the future, once we're either using `sqlite3_stmt` directly or have replaced `Microsoft.Data.Sqlite`
		// with a custom version more suited to our needs, rent/stackalloc a buffer for the hash output instead, then
		// pass that as a `ReadOnlySpan<byte>` to `sqlite3_bind_blob()`.
		return usum.ToArray();
	}

	[Conditional("DEBUG")]
	private static void DAssert_BareSchemaUsum(byte[] usum)
		=> DAssert_SchemaUsum_HasSharedDataBit(usum, hasSharedData: false);

	[Conditional("DEBUG")]
	private static void DAssert_NonBareSchemaUsum(byte[] usum)
		=> DAssert_SchemaUsum_HasSharedDataBit(usum, hasSharedData: true);

	[Conditional("DEBUG")]
	private static void DAssert_SchemaUsum_HasSharedDataBit(byte[] usum, bool hasSharedData) {
		Debug.Assert(usum != null);
		Debug.Assert(usum.Length == SchemaUsumDigestLength);

		// Expect version 1 (since the check after is valid only for that)
		Debug.Assert(usum[0] == 1);
		// Expect that the "has shared data" bit flag matches
		Debug.Assert((usum[1] & 1) == hasSharedData.ToByte());
	}

	// --

	[DoesNotReturn]
	private static void E_InvalidFieldCounts_InvDat(long schemaId, int xhc, int xlc, int xsc) {
		throw new InvalidDataException(
			$"Schema (with rowid {schemaId}) has {xhc} as its maximum hot " +
			$"field count while having {xlc} as its maximum local field " +
			$"count, with {xsc} as its maximum shared field count." + (
				Math.Max(xsc, xlc) <= MaxFieldCount ? "" : Environment.NewLine +
			$"Note that, one of them exceeds {MaxFieldCount}, the maximum " +
			$"allowed count."));
	}

	[DoesNotReturn]
	private static void E_MissingLocalField_InvDat(long schemaId) {
		throw new InvalidDataException(
			$"Schema (with rowid {schemaId}) is missing a local field " +
			$"definition.");
	}

	[DoesNotReturn]
	private static void E_MissingSharedField_InvDat(long schemaId) {
		throw new InvalidDataException(
			$"Schema (with rowid {schemaId}) is missing a shared field " +
			$"definition.");
	}

	[DoesNotReturn]
	private static void E_IndexBeyondSharedFieldCount_InvDat(long schemaId, int i, int xsc) {
		Debug.Assert(xsc >= 0);
		Debug.Assert((uint)i >= (uint)xsc);

		throw new InvalidDataException(
			$"Schema (with rowid {schemaId}) gave an invalid shared field " +
			$"index {i}, which is " + (i < 0 ? "negative." : "not under " +
			$"{xsc}, the expected maximum number of shared fields defined " +
			$"by the schema."));
	}

	// --

	[DoesNotReturn]
	private void E_TooManyFields(int count) {
		Debug.Assert(count > MaxFieldCount);
		throw new InvalidOperationException(
			$"Total number of fields (currently {count}) shouldn't exceed {MaxFieldCount};" +
			$"{Environment.NewLine}Entity: {GetDebugLabel()};" +
			$"{Environment.NewLine}Base Schema: {_SchemaId};");
	}

	[DoesNotReturn]
	private void E_TooManyClasses(int count) {
		Debug.Assert(count > MaxClassCount);
		throw new InvalidOperationException(
			$"Total number of classes (currently {count}) shouldn't exceed {MaxClassCount};" +
			$"{Environment.NewLine}Entity: {GetDebugLabel()};" +
			$"{Environment.NewLine}Base Schema: {_SchemaId};");
	}
}
