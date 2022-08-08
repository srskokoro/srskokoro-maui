namespace Kokoro.Internal;
using Blake2Fast;
using Blake2Fast.Implementation;
using Kokoro.Common.Buffers;
using Kokoro.Common.Util;
using Kokoro.Internal.Sqlite;
using Microsoft.Data.Sqlite;
using System.Buffers;
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
			public FieldSpec src_idx_sto;

			public string name;
			public FieldVal? new_fval;
			public UniqueId cls_uid; // TODO Assess if still necessary

			public FieldInfo(
				long rowid,
				int cls_ord, FieldStoreType sto, int ord, FieldSpec src_idx_sto,
				string name, FieldVal? new_fval, UniqueId cls_uid
			) {
				U.SkipInit(out this);
				this.rowid = rowid;

				this.cls_ord = cls_ord;
				this.sto = sto;
				this.ord = ord;
				this.src_idx_sto = src_idx_sto;

				this.name = name;
				this.new_fval = new_fval;
				this.cls_uid = cls_uid;
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
	}

	/// <remarks>
	/// CONTRACT:
	/// <br/>- Must be called while inside a transaction (ideally, using <see cref="NestingWriteTransaction"/>).
	/// <br/>- Must load <see cref="_SchemaId"/> beforehand, at least once,
	/// while inside the transaction.
	/// <para>
	/// Violation of the above contract may result in undefined behavior.
	/// </para>
	/// </remarks>
	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	[SkipLocalsInit]
	private protected void RewriteSchema(ref FieldsReader fr, int hotStoreLimit, ref FieldsWriter fw) {
		DAssert_FieldsWriterPriorRewrite(ref fw);

		var clsSet = _Classes;
		// ^- NOTE: Soon, the class set will contain only the newly added
		// classes (i.e., classes awaiting addition). The code after will make
		// sure that happens. Later, it'll also include direct classes from the
		// old schema, provided that those awaiting removal are filtered out.
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
				clsChgSet = clsSet._Changes!;
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

			// Get the old schema's direct classes
			// --

			db = fr.Db;
			using var cmd = db.CreateCommand();
			cmd.Set("SELECT cls FROM SchemaToClass WHERE (ind,schema)=(0,$schema)")
				.AddParams(new("$schema", _SchemaId));

			using var r = cmd.ExecuteReader();
			while (r.Read()) {
				r.DAssert_Name(0, "cls");
				long cls = r.GetInt64(0);

				if (!clsChgSet.Contains(cls))
					clsSet.Add(cls);
			}
		}

		int dclsCount = clsSet.Count;
		if (dclsCount > MaxClassCount) goto E_TooManyClasses;

		List<SchemaRewrite.ClassInfo> clsList = new(dclsCount);

		// Get the needed info for each class while also gathering all the
		// indirect classes
		using (var clsCmd = db.CreateCommand()) {
			SqliteParameter clsCmd_rowid;
			clsCmd.Set("SELECT uid,csum,ord FROM Class WHERE rowid=$rowid")
				.AddParams(clsCmd_rowid = new() { ParameterName = "$rowid" });

			// Get the needed info for each direct class
			foreach (long cls in clsSet) {
				clsCmd_rowid.Value = cls;

				using var r = clsCmd.ExecuteReader();
				if (r.Read()) {
					r.DAssert_Name(0, "uid");
					UniqueId uid = r.GetUniqueId(0);

					r.DAssert_Name(1, "csum");
					byte[] csum = r.GetBytes(1);

					r.DAssert_Name(2, "ord");
					int ord = r.GetInt32(2);

					clsList.Add(new(rowid: cls, uid, csum, ord));
				} else {
					// User attached a nonexistent class to the fielded entity.
					// Ignore it then.
				}
			}

			SqliteParameter inclCmd_cls;
			using var inclCmd = db.CreateCommand();
			inclCmd.Set("SELECT incl FROM ClassToInclude WHERE cls=$cls")
				.AddParams(inclCmd_cls = new() { ParameterName = "$cls" });

			// Recursively get all included classes, skipping already seen
			// classes to prevent infinite recursion
			for (int i = 0; i < clsList.Count; i++) {
				ref var cls = ref clsList.AsSpan().DangerousGetReferenceAt(i);
				inclCmd_cls.Value = cls.rowid;

				using var inclCmd_r = inclCmd.ExecuteReader();
				while (inclCmd_r.Read()) {
					inclCmd_r.DAssert_Name(0, "incl");
					long incl = inclCmd_r.GetInt64(0);

					if (clsSet.Add(incl)) {
						// Get the needed info for the indirect class
						// --
						clsCmd_rowid.Value = incl;

						using var r = clsCmd.ExecuteReader();
						if (r.Read()) {
							r.DAssert_Name(0, "uid");
							UniqueId uid = r.GetUniqueId(0);

							r.DAssert_Name(1, "csum");
							byte[] csum = r.GetBytes(1);

							r.DAssert_Name(2, "ord");
							int ord = r.GetInt32(2);

							clsList.Add(new(rowid: incl, uid, csum, ord));
						} else {
							Debug.Fail("An FK constraint should've been " +
								"enforced to ensure this doesn't happen.");
						}
					}
				}
			}
		}

		if (clsSet.Count > MaxClassCount) goto E_TooManyClasses;

		// Gather old field mappings from the old schema, by mapping a field id
		// to a pair of field change value and old field spec.
		// --

		Dictionary<long, (FieldVal? FVal, FieldSpec FSpec)> fldMapOld;
		{
			// Process the field changes
			// --

			Fields? fields = _Fields;
			if (fields == null) goto NoFieldChanges;

			FieldChanges? fchanges = fields._Changes;
			if (fchanges == null) goto NoFieldChanges;

			var fchanges_iter = fchanges.GetEnumerator();
			if (!fchanges_iter.MoveNext()) goto NoFieldChanges;

			fldMapOld = new(fchanges.Count);

			db.ReloadNameIdCaches(); // Needed by `db.LoadStale…()` below
			do {
				var (fname, fval) = fchanges_iter.Current;
				long fld = db.LoadStaleOrEnsureNameId(fname);

				bool added = fldMapOld.TryAdd(fld, (fval, -1));
				Debug.Assert(added, $"Shouldn't happen if running in a " +
					$"`{nameof(NestingWriteTransaction)}` or equivalent");
			} while (fchanges_iter.MoveNext());

			Debug.Assert(fldMapOld.Count == fchanges.Count);

			// --
			goto DoneWithFieldChanges;

		NoFieldChanges:
			fldMapOld = new();

		DoneWithFieldChanges:
			;

			// Retrieve the field specs from the old schema
			// --

			using (var cmd = db.CreateCommand()) {
				cmd.Set("SELECT fld,idx_sto FROM SchemaToField WHERE schema=$schema")
					.AddParams(new("$schema", _SchemaId));

				using var r = cmd.ExecuteReader();
				while (r.Read()) {
					r.DAssert_Name(0, "fld");
					long fld = r.GetInt64(0);

					r.DAssert_Name(1, "idx_sto");
					FieldSpec fspec = r.GetInt32(1);
					Debug.Assert((int)fspec >= 0);

					ref var entry = ref CollectionsMarshal.GetValueRefOrAddDefault(
						fldMapOld, fld, out _
					);
					entry.FSpec = fspec;
				}
			}
		}

		// Gather the fields associated with each class, skipping duplicates,
		// while also fetching any needed info
		// --

		List<SchemaRewrite.FieldInfo> fldList = new();

		int fldSharedCount = 0;
		int fldHotCount = 0;

		using (var cmd = db.CreateCommand()) {
			SqliteParameter cmd_cls;
			cmd.Set(
				"SELECT\n" +
					"fld.rowid AS fld,\n" +
					"fld.name AS name,\n" +
					"cls2fld.ord AS ord,\n" +
					"cls2fld.sto AS sto\n" +
				"FROM ClassToField AS cls2fld,NameId AS fld\n" +
				"WHERE cls2fld.cls=$cls AND fld.rowid=cls2fld.fld"
			).AddParams(
				cmd_cls = new() { ParameterName = "$cls" }
			);

			// Used only to spot duplicate entries
			Dictionary<long, int> fldMap = new();

			foreach (ref var cls in clsList.AsSpan()) {
				cmd_cls.Value = cls.rowid;

				using var r = cmd.ExecuteReader();
				while (r.Read()) {
					r.DAssert_Name(0, "fld");
					long fld = r.GetInt64(0);

					r.DAssert_Name(1, "name");
					string name = r.GetString(1);

					r.DAssert_Name(2, "ord");
					int ord = r.GetInt32(2);

					r.DAssert_Name(3, "sto");
					FieldStoreType sto = (FieldStoreType)r.GetInt32(3);
					sto.DAssert_Defined();

					ref int i = ref CollectionsMarshal.GetValueRefOrAddDefault(fldMap, fld, out bool exists);
					if (!exists) {
						i = fldList.Count; // The new entry's index in the list

						FieldVal? new_fval;
						FieldSpec src_idx_sto;

						if (fldMapOld.Remove(fld, out var oldMapping)) {
							(new_fval, src_idx_sto) = oldMapping;
							Debug.Assert(new_fval != null || (int)src_idx_sto >= 0);
						} else {
							// Field not defined by the old schema
							new_fval = OnSupplantFloatingField(db, fld) ?? FieldVal.Null;
							src_idx_sto = -1;
						}

						// Add the new entry
						fldList.Add(new(
							rowid: fld,
							cls_ord: cls.ord, sto, ord, src_idx_sto,
							name, new_fval, cls_uid: cls.uid
						));
					} else {
						// Get a `ref` to the already existing entry
						ref var entry = ref fldList.AsSpan().DangerousGetReferenceAt(i);

						Debug.Assert(name == entry.name, $"Impossible! " +
							$"Two fields have the same name id (which is {fld}) but different names:{Environment.NewLine}" +
							$"Name of 1st instance: {name}{Environment.NewLine}" +
							$"Name of 2nd instance: {entry.name}");

						// Attempt to replace existing entry
						// --
						{
							var a = cls.ord; var b = entry.cls_ord;
							if (a != b) {
								if (a < b) goto ReplaceEntry;
								else goto LeaveEntry;
							}
						}
						{
							var a = sto; var b = entry.sto;
							if (a != b) {
								if (a < b) goto ReplaceEntry;
								else goto LeaveEntry;
							}
						}
						{
							var a = ord; var b = entry.ord;
							if (a != b) {
								if (a < b) goto ReplaceEntry;
								else goto LeaveEntry;
							}
						}
						{
							var a = cls.uid; var b = entry.cls_uid;
							if (a != b) {
								if (a < b) goto ReplaceEntry;
								else goto LeaveEntry;
							}
						}
						{
							// Same field. Same class. It's the same entry.
							Debug.Fail($"Unexpected! Duplicate rows returned for class-and-field pair:{Environment.NewLine}" +
								$"Class UID: {cls.uid}{Environment.NewLine}" +
								$"Field Name: {name}{Environment.NewLine}" +
								$"Field ROWID: {fld}");

							// It's the same entry anyway.
							goto LeaveEntry;
						}

					ReplaceEntry:
						{
							var entry_sto = entry.sto;
							if (entry_sto != FieldStoreType.Shared) {
								if (entry_sto == FieldStoreType.Hot) {
									fldHotCount--;
								}
							} else {
								fldSharedCount--;
							}
						}
						entry = new(
							rowid: fld,
							cls_ord: cls.ord, sto, ord, entry.src_idx_sto,
							name, entry.new_fval, cls_uid: cls.uid
						);
					}

					Debug.Assert(typeof(FieldStoreTypeInt) == typeof(FieldStoreTypeUInt));

					if (sto != FieldStoreType.Shared) {
						if (sto == FieldStoreType.Hot) {
							fldHotCount++;
						}
					} else {
						fldSharedCount++;
					}

				LeaveEntry:
					;
				}
			}
		}

		Debug.Assert((uint)fldSharedCount <= (uint)fldList.Count);
		Debug.Assert((uint)fldHotCount <= (uint)fldList.Count);

		Debug.Assert(checked(fldSharedCount + fldHotCount) <= fldList.Count);

		if (fldList.Count > MaxFieldCount) goto E_TooManyFields;

		// The remaining old field mappings will become floating fields -- i.e.,
		// fields not defined by the (new) schema.
		{
			int fldMapOldCount = fldMapOld.Count;
			if (fldMapOldCount != 0) {
				var floFlds = fw._FloatingFields;
				if (floFlds == null) {
					floFlds = new(fldMapOldCount);
				} else {
					floFlds.EnsureCapacity(fldMapOldCount);
				}
				foreach (var (fld, entry) in fldMapOld) {
					var fval = entry.FVal;
					floFlds.Add((fld, new(fval, fval != null
						? default : fr.ReadLater(entry.FSpec))));
				}
			}
		}

		// -=-
		{
			Debug.Assert(fldList.Count <= byte.MaxValue + 1);
			Debug.Assert(clsList.Count <= byte.MaxValue + 1);

			[MethodImpl(MethodImplOptions.AggressiveOptimization)]
			static void InitWithIndices(Span<byte> span) {
				ref var r0 = ref span.DangerousGetReference();
				for (int i = span.Length; --i >= 0;) {
					U.Add(ref r0, i) = (byte)i;
				}
				Debug.Assert(span.Length <= 0 || (
					U.Add(ref r0, 0) == 0 &&
					U.Add(ref r0, span.Length/2) == span.Length/2 &&
					U.Add(ref r0, span.Length-1) == span.Length-1
				));
			}

			using var renter_fldListIdxs = BufferRenter<byte>
				.CreateSpan(fldList.Count, out var fldListIdxs);
			InitWithIndices(fldListIdxs);

			using var renter_clsListIdxs = BufferRenter<byte>
				.CreateSpan(clsList.Count, out var clsListIdxs);
			InitWithIndices(clsListIdxs);

			// Sort the gathered lists by sorting their indices instead
			{
				// TODO Perhaps utilize `[ThreadStatic]` to avoid allocations
				SchemaRewrite.Comparisons comparisons = new(fldList, clsList);

				// Sort the list of fields
				// --

				// Assumed implementation: The sorted array will be partitioned
				// by field store type.
				fldListIdxs.Sort(comparisons.fldList_compare);

				// Partition the list of classes into two, direct and indirect
				// classes, then sort each partition separately.
				// --

				Comparison<byte> clsList_compare = comparisons.clsList_compare;

				// Sort the list of direct classes
				clsListIdxs[..dclsCount].Sort(clsList_compare);

				// Sort the list of indirect classes
				clsListIdxs[dclsCount..].Sort(clsList_compare);
			}

			DAssert_fldListIdxs_AssumedLayoutIsCorrect(); // Future-proofing

			[Conditional("DEBUG")]
			static void DAssert_fldListIdxs_AssumedLayoutIsCorrect() {
				Span<FieldStoreType> expected = stackalloc FieldStoreType[] {
					FieldStoreType.Shared,
					FieldStoreType.Hot,
					FieldStoreType.Cold,
				};

				Span<FieldStoreType> actual = stackalloc FieldStoreType[expected.Length];
				expected.CopyTo(actual);

				actual.Sort();

				bool eq = expected.SequenceEqual(actual);
				Debug.Assert(eq, $"Arrangement of values in `{nameof(fldListIdxs)}` may not be as expected.");
			}

			// --

			// NOTE: The ending portion of this buffer will be used to store the
			// shared field offsets.
			ref var offsets_r0 = ref (fw._Offsets = ArrayPool<int>.Shared.Rent(fldListIdxs.Length)).DangerousGetReference();

			int fldLocalCount = fldListIdxs.Length - fldSharedCount;
			int nextOffset;

			// Rewrite local fields
			{
				int nlc = fldLocalCount;
				Debug.Assert(nlc >= 0);
				if (nlc == 0) goto ClearLocalFields;

				// NOTE: Client code will ensure that the rented buffers are
				// returned, even when rewriting fails (i.e., even when we don't
				// return normally).
				ref var entries_r0 = ref (fw._Entries = ArrayPool<FieldsWriter.Entry>.Shared.Rent(nlc)).DangerousGetReference();

				Debug.Assert((uint)nlc <= (uint)fldListIdxs.Length);
				Debug.Assert(fldList.Count == fldListIdxs.Length);
				ref var fldList_r0 = ref fldList.AsSpan().DangerousGetReference();
				ref byte fldListIdxs_r0 = ref fldListIdxs.DangerousGetReferenceAt(fldSharedCount);

				nextOffset = 0;
				try {
					int i = 0;
					do {
						U.Add(ref offsets_r0, i) = nextOffset;
						ref var entry = ref U.Add(ref entries_r0, i);

						int j = U.Add(ref fldListIdxs_r0, i);
						Debug.Assert((uint)j < (uint)fldList.Count);
						ref var fld = ref U.Add(ref fldList_r0, j);

						FieldVal? fval = fld.new_fval;
						if (fval == null) {
							LatentFieldVal lfval = fr.ReadLater(fld.src_idx_sto);
							entry.OrigValue = lfval;

							// It's a reference type: it should've been
							// automatically initialized to null.
							Debug.Assert(entry.Override == null);

							int nextLength = lfval.Length;
							if (nextLength >= 0) {
								checked { nextOffset += nextLength; }
							} else {
								entry.Override = FieldVal.Null;
							}
						} else {
							entry.Override = fval;

							checked {
								nextOffset += (int)fval.CountEncodeLength();
							}
						}
					} while (++i < nlc);
				} catch (OverflowException) {
					goto E_FieldValsLengthTooLarge;
				}

				if ((uint)nextOffset <= (uint)MaxFieldValsLength) {
					nlc = FieldsWriterCore.TrimNullFValsFromEnd(ref entries_r0, nlc);
				} else {
					goto E_FieldValsLengthTooLarge;
				}

				if (nlc == 0) goto ClearLocalFields;

				int lastFOffsetSizeM1Or0 = (
					(uint)U.Add(ref offsets_r0, nlc-1)
				).CountBytesNeededM1Or0();

				int nhc = fldHotCount;

				// NOTE: `nlc < nhc` may happen due to trimmed null fields.
				if ((uint)nlc <= (uint)nhc) {
					FieldsDesc hotFDesc = new(
						fCount: nlc,
						fOffsetSizeM1Or0: lastFOffsetSizeM1Or0
					);

					fw._HotFieldsDesc = hotFDesc;

					// NOTE: The first offset value is never stored, as it'll
					// always be zero otherwise.
					Debug.Assert(nlc > 0); // Future-proofing
					fw._HotStoreLength = VarInts.Length(hotFDesc)
						+ (nlc - 1) * (lastFOffsetSizeM1Or0 + 1)
						+ nextOffset;

					fw._ColdStoreLength = 0;

				} else {
					Debug.Assert(nlc > nhc, $"Needs at least 1 cold field loaded"); // Future-proofing

					int hotFValsSize = U.Add(ref offsets_r0, nhc);
					int hotFOffsetSizeM1Or0 = nhc == 0 ? 0 : (
						(uint)U.Add(ref offsets_r0, nhc-1)
					).CountBytesNeededM1Or0();

					FieldsDesc hotFDesc = new(
						fCount: nhc,
						fHasCold: true,
						fOffsetSizeM1Or0: hotFOffsetSizeM1Or0
					);

					fw._HotFieldsDesc = hotFDesc;

					// NOTE: The first offset value is never stored, as it'll
					// always be zero otherwise.
					fw._HotStoreLength = VarInts.Length(hotFDesc)
						+ (nhc - 1).NonNegOrBitCompl() * (hotFOffsetSizeM1Or0 + 1)
						+ hotFValsSize;

					int coldFOffsetSizeM1Or0 = (
						(uint)(lastFOffsetSizeM1Or0 - hotFValsSize)
					).CountBytesNeededM1Or0();

					int ncc = nlc - nhc;
					FieldsDesc coldFDesc = new(
						fCount: ncc,
						fOffsetSizeM1Or0: coldFOffsetSizeM1Or0
					);

					fw._ColdFieldsDesc = coldFDesc;

					// NOTE: The first offset value is never stored, as it'll
					// always be zero otherwise.
					Debug.Assert(ncc > 0); // Future-proofing
					fw._ColdStoreLength = VarInts.Length(coldFDesc)
						+ (ncc - 1) * (coldFOffsetSizeM1Or0 + 1)
						+ (nextOffset - hotFValsSize);
				}

				// Done!
				goto DoneWithLocalFields;
			}

		ClearLocalFields:
			{
				fw._HotFieldsDesc = FieldsDesc.Empty;
				fw._HotStoreLength = FieldsDesc.VarIntLengthForEmpty;
				fw._ColdStoreLength = 0;
				goto DoneWithLocalFields;
			}

		DoneWithLocalFields:
			;

			byte[] usum;

			// Generate the schema `usum`
			{
				var hasher = Blake2b.CreateIncrementalHasher(SchemaUsumDigestLength);
				// WARNING: The expected order of inputs to be fed to the above
				// hasher must be strictly as follows:
				//
				// 0. The number of direct classes, as a 32-bit integer.
				// 1. The list of `csum`s from direct classes, ordered by `uid`.
				// 2. The number of indirect classes, as a 32-bit integer.
				// 3. The list of `csum`s from indirect classes, ordered by `uid`.
				// 4. The number of shared fields, including trailing null
				// fields, as a 32-bit integer.
				// 5. The field values of shared fields, including trailing null
				// fields, each prepended with its length.
				//
				// Unless stated otherwise, all integer inputs should be
				// consumed in their little-endian form.
				//
				// The resulting hash BLOB shall be prepended with a version
				// varint. Should any of the following happens, the version
				// varint must change:
				//
				// - The resulting hash BLOB length changes.
				// - The algorithm for the resulting hash BLOB changes.
				// - An input entry (from the list of inputs above) was removed.
				// - The order of an input entry (from the list of inputs above)
				// was changed or shifted.
				// - An input entry's size (in bytes) changed while it's
				// expected to be fixed-size (e.g., not length-prepended).
				//
				// The version varint needs not to change if further input
				// entries were to be appended (from the list of inputs above).

				// Hash the list of direct class's `csum`
				{
					Debug.Assert(dclsCount >= 0);
					hasher.UpdateLE(dclsCount); // i.e., length-prepended
					if (dclsCount == 0) goto Hashed;

					int i = 0;
					int n = dclsCount;

					Debug.Assert((uint)n <= (uint)clsListIdxs.Length);
					Debug.Assert(clsList.Count == clsListIdxs.Length);
					ref var clsList_r0 = ref clsList.AsSpan().DangerousGetReference();
					ref byte clsListIdxs_r0 = ref clsListIdxs.DangerousGetReference();

					do {
						int j = U.Add(ref clsListIdxs_r0, i);

						Debug.Assert((uint)j < (uint)clsList.Count);
						ref var cls = ref U.Add(ref clsList_r0, j);

						hasher.Update(cls.csum);
					} while (++i < n);

				Hashed:
					;
				}

				// Hash the list of indirect class's `csum`
				{
					int i = dclsCount;
					int n = clsListIdxs.Length;

					int iclsCount = n - i;
					Debug.Assert(iclsCount >= 0);
					hasher.UpdateLE(iclsCount); // i.e., length-prepended
					if (iclsCount == 0) goto Hashed;

					Debug.Assert(clsList.Count == clsListIdxs.Length);
					ref var clsList_r0 = ref clsList.AsSpan().DangerousGetReference();
					ref byte clsListIdxs_r0 = ref clsListIdxs.DangerousGetReference();

					do {
						int j = U.Add(ref clsListIdxs_r0, i);

						Debug.Assert((uint)j < (uint)clsList.Count);
						ref var cls = ref U.Add(ref clsList_r0, j);

						hasher.Update(cls.csum);
					} while (++i < n);

				Hashed:
					;
				}

				// Hash the shared fields' values (length-prepend each), while
				// also gathering the shared fields' offsets.
				{
					nextOffset = 0;

					Debug.Assert(fldSharedCount >= 0);
					hasher.UpdateLE(fldSharedCount); // i.e., length-prepended
					if (fldSharedCount == 0) goto Processed;

					int i = 0;
					int n = fldSharedCount;

					Debug.Assert((uint)n <= (uint)fldListIdxs.Length);
					Debug.Assert(fldList.Count == fldListIdxs.Length);
					ref var fldList_r0 = ref fldList.AsSpan().DangerousGetReference();
					ref byte fldListIdxs_r0 = ref fldListIdxs.DangerousGetReference();

					// The shared fields' offsets
					ref int shared_offsets_r0 = ref U.Add(ref offsets_r0, fldListIdxs.Length - fldSharedCount);

					try {
						do {
							U.Add(ref shared_offsets_r0, i) = nextOffset;
							int j = U.Add(ref fldListIdxs_r0, i);

							Debug.Assert((uint)j < (uint)fldList.Count);
							ref var fld = ref U.Add(ref fldList_r0, j);

							FieldVal? fval = fld.new_fval;
							if (fval == null) {
								LatentFieldVal lfval = fr.ReadLater(fld.src_idx_sto);
								int nextLength = lfval.Length;
								if (nextLength >= 0) {
									checked { nextOffset += nextLength; }
								}
								lfval.FeedTo(ref hasher);
							} else {
								checked {
									nextOffset += (int)fval.CountEncodeLength();
								}
								fval.FeedTo(ref hasher);
							}
						} while (++i < n);
					} catch (OverflowException) {
						goto E_FieldValsLengthTooLarge;
					}

				Processed:
					;
				}

				// --

				if ((uint)nextOffset <= (uint)MaxFieldValsLength) {
					fldLocalCount = fldListIdxs.Length - fldSharedCount;

					Debug.Assert((uint)fldLocalCount
						<= (uint)MaxFieldCount && (uint)MaxFieldCount
						<= (uint)byte.MaxValue);

					usum = FinishWithSchemaUsum(ref hasher, (byte)fldLocalCount);
				} else {
					goto E_FieldValsLengthTooLarge;
				}
			}

			int sharedFValsSize = nextOffset;

			// --

			// Look up new schema rowid given `usum`
			using (var cmd = db.CreateCommand()) {
				cmd.Set("SELECT rowid FROM Schema WHERE usum=$usum")
					.AddParams(new("$usum", usum));

				using var r = cmd.ExecuteReader();
				if (r.Read()) {
					// Existing schema found!

					r.DAssert_Name(0, "rowid");
					long schemaId = r.GetInt64(0);

					Debug.Assert(schemaId != 0);
					_SchemaId = schemaId;

					return; // Early exit
				}
			}

			// Insert new schema entry
			// --

			Debug.Assert(db.Context != null);
			long newSchemaId = db.Context.NextSchemaId();

			try {
				// Save the class entries for the new schema
				// --

				// Save the direct classes
				{
					Debug.Assert(dclsCount >= 0);
					if (dclsCount == 0) goto Saved;

					int i = 0;
					int n = dclsCount;

					Debug.Assert((uint)n <= (uint)clsListIdxs.Length);
					Debug.Assert(clsList.Count == clsListIdxs.Length);
					ref var clsList_r0 = ref clsList.AsSpan().DangerousGetReference();
					ref byte clsListIdxs_r0 = ref clsListIdxs.DangerousGetReference();

					using (var cmd = db.CreateCommand()) {
						SqliteParameter cmd_cls, cmd_csum;

						cmd.Set(
							"INSERT INTO SchemaToClass" +
							"(schema,cls,csum,ind)" +
							"\nVALUES" +
							"($schema,$cls,$csum,0)"
						).AddParams(
							new("$schema", newSchemaId),
							cmd_cls = new() { ParameterName = "$cls" },
							cmd_csum = new() { ParameterName = "$csum" }
						);

						do {
							int j = U.Add(ref clsListIdxs_r0, i);

							Debug.Assert((uint)j < (uint)clsList.Count);
							ref var cls = ref U.Add(ref clsList_r0, j);

							cmd_cls.Value = cls.rowid;
							cmd_csum.Value = cls.csum;

							int updated = cmd.ExecuteNonQuery();
							Debug.Assert(updated == 1, $"Updated: {updated}");
						} while (++i < n);
					}

				Saved:
					;
				}

				// Save the indirect classes
				{
					int i = dclsCount;
					int n = clsListIdxs.Length;

					int iclsCount = n - i;
					Debug.Assert(iclsCount >= 0);
					if (iclsCount == 0) goto Saved;

					Debug.Assert(clsList.Count == clsListIdxs.Length);
					ref var clsList_r0 = ref clsList.AsSpan().DangerousGetReference();
					ref byte clsListIdxs_r0 = ref clsListIdxs.DangerousGetReference();

					using (var cmd = db.CreateCommand()) {
						SqliteParameter cmd_cls, cmd_csum;

						cmd.Set(
							"INSERT INTO SchemaToClass" +
							"(schema,cls,csum,ind)" +
							"\nVALUES" +
							"($schema,$cls,$csum,1)"
						).AddParams(
							new("$schema", newSchemaId),
							cmd_cls = new() { ParameterName = "$cls" },
							cmd_csum = new() { ParameterName = "$csum" }
						);

						do {
							int j = U.Add(ref clsListIdxs_r0, i);

							Debug.Assert((uint)j < (uint)clsList.Count);
							ref var cls = ref U.Add(ref clsList_r0, j);

							cmd_cls.Value = cls.rowid;
							cmd_csum.Value = cls.csum;

							int updated = cmd.ExecuteNonQuery();
							Debug.Assert(updated == 1, $"Updated: {updated}");
						} while (++i < n);
					}

				Saved:
					;
				}

				// Save the field infos for the new schema
				// --

				if (fldListIdxs.Length != 0) {
					using var cmd = db.CreateCommand();
					SqliteParameter cmd_fld, cmd_idx_sto;

					cmd.Set(
						"INSERT INTO SchemaToField" +
						"(schema,fld,idx_sto)" +
						"\nVALUES" +
						"($schema,$fld,$idx_sto)"
					).AddParams(
						new("$schema", newSchemaId),
						cmd_fld = new() { ParameterName = "$fld" },
						cmd_idx_sto = new() { ParameterName = "$idx_sto" }
					);

					Debug.Assert(fldList.Count == fldListIdxs.Length);
					ref var fldList_r0 = ref fldList.AsSpan().DangerousGetReference();
					ref byte fldListIdxs_r0 = ref fldListIdxs.DangerousGetReference();

					Debug.Assert((uint)fldSharedCount <= (uint)fldListIdxs.Length);

					SaveFieldInfos(
						cmd,
						cmd_fld: cmd_fld,
						cmd_idx_sto: cmd_idx_sto,

						ref fldList_r0,
						ref fldListIdxs_r0,

						start: 0, end: fldSharedCount,
						FieldStoreType.Shared
					);

					fldListIdxs_r0 = ref U.Add(ref fldListIdxs_r0, fldSharedCount);
					Debug.Assert((uint)(fldSharedCount + fldHotCount) <= (uint)fldListIdxs.Length);

					SaveFieldInfos(
						cmd,
						cmd_fld: cmd_fld,
						cmd_idx_sto: cmd_idx_sto,

						ref fldList_r0,
						ref fldListIdxs_r0,

						start: 0, end: fldHotCount,
						FieldStoreType.Hot
					);

					Debug.Assert((uint)fldHotCount
						<= (uint)fldLocalCount && (uint)fldLocalCount
						<= (uint)fldListIdxs.Length);

					SaveFieldInfos(
						cmd,
						cmd_fld: cmd_fld,
						cmd_idx_sto: cmd_idx_sto,

						ref fldList_r0,
						ref fldListIdxs_r0,

						start: fldHotCount, end: fldLocalCount,
						FieldStoreType.Cold
					);

					[MethodImpl(MethodImplOptions.AggressiveInlining)]
					static void SaveFieldInfos(
						SqliteCommand cmd,
						SqliteParameter cmd_fld,
						SqliteParameter cmd_idx_sto,

						ref SchemaRewrite.FieldInfo fldList_r0,
						ref byte fldListIdxs_r0,

						int start, int end,
						FieldStoreType storeType
					) {
						if (start < end) {
							Debug.Assert((uint)end
								<= (uint)MaxFieldCount && (uint)MaxFieldCount
								<= (uint)FieldSpec.MaxIndex);

							FieldSpec idx_sto_c = new(start, storeType);
							FieldSpec idx_sto_n = new(end, storeType);

							do {
								int j = U.Add(ref fldListIdxs_r0, idx_sto_c.Index);

								cmd_fld.Value = U.Add(ref fldList_r0, j).rowid;
								cmd_idx_sto.Value = idx_sto_c;

								int updated = cmd.ExecuteNonQuery();
								Debug.Assert(updated == 1, $"Updated: {updated}");

							} while ((idx_sto_c += FieldSpec.IndexIncrement).Value < idx_sto_n.Value);
						}
					}
				}

				// Establish the new schema entry
				// --
				{
					byte[] data;

					// NOTE: In order to simplify implementation, we don't trim
					// trailing null shared fields.

					if (fldSharedCount > 0) {
						int nsc = fldSharedCount;
						Debug.Assert((uint)nsc <= (uint)fldListIdxs.Length);
						ref int shared_offsets_r0 = ref U.Add(ref offsets_r0, fldListIdxs.Length - nsc);

						int sharedFOffsetSizeM1Or0 = (
							(uint)U.Add(ref shared_offsets_r0, nsc-1)
						).CountBytesNeededM1Or0();

						FieldsDesc sharedFDesc = new(
							fCount: nsc,
							fOffsetSizeM1Or0: sharedFOffsetSizeM1Or0
						);

						Span<byte> buffer_sharedFDesc = stackalloc byte[VarInts.MaxLength32];
						buffer_sharedFDesc = buffer_sharedFDesc[
							..VarInts.Write(buffer_sharedFDesc, sharedFDesc)];

						int sharedFOffsetSize = sharedFOffsetSizeM1Or0 + 1;

						// NOTE: The first offset value is never stored, as
						// it'll always be zero otherwise.
						int sharedStoreLength = buffer_sharedFDesc.Length
							+ (nsc - 1) * sharedFOffsetSize
							+ sharedFValsSize;

						data = GC.AllocateUninitializedArray<byte>(sharedStoreLength);
						MemoryStream dest = new(data);

						dest.Write(buffer_sharedFDesc);

						// Write the field offsets
						// --

						int i = 0;
						do {
							dest.WriteUInt32AsUIntX(
								(uint)U.Add(ref shared_offsets_r0, i),
								sharedFOffsetSize);
						} while (++i < nsc);

						// Write the field values
						// --

						Debug.Assert(fldList.Count == fldListIdxs.Length);
						ref var fldList_r0 = ref fldList.AsSpan().DangerousGetReference();
						ref byte fldListIdxs_r0 = ref fldListIdxs.DangerousGetReference();

						i = 0;
						do {
							int j = U.Add(ref fldListIdxs_r0, i);

							Debug.Assert((uint)j < (uint)fldList.Count);
							ref var fld = ref U.Add(ref fldList_r0, j);

							FieldVal? fval = fld.new_fval;
							if (fval == null) {
								var lfval = fr.Read(fld.src_idx_sto);
								lfval.WriteTo(dest);
							} else {
								fval.WriteTo(dest);
							}
						} while (++i < nsc);

					} else {
						Debug.Assert(fldSharedCount == 0);
						data = SchemaRewrite.DataWhenNoSharedFields.ReadOnlyBytes;
					}

					using (var cmd = db.CreateCommand()) {
						cmd.Set(
							"INSERT INTO Schema" +
							"(rowid,usum,hotCount,coldCount,data)" +
							"\nVALUES" +
							"($rowid,$usum,$hotCount,$coldCount,$data)"
						).AddParams(
							new("$rowid", newSchemaId),
							new("$usum", usum),
							new("$hotCount", fldHotCount),
							new("$coldCount", fldLocalCount - fldHotCount),
							new("$data", data)
						);

						int updated = cmd.ExecuteNonQuery();
						Debug.Assert(updated == 1, $"Updated: {updated}");
					}
				}

			} catch (Exception ex) when (
				ex is not SqliteException sqlex ||
				sqlex.SqliteExtendedErrorCode != SQLitePCL.raw.SQLITE_CONSTRAINT_ROWID
			) {
				db.Context?.UndoSchemaId(newSchemaId);
				throw;
			}

			// Success!
			_SchemaId = newSchemaId;

			return; // Early exit

		E_FieldValsLengthTooLarge:
			E_FieldValsLengthTooLarge((uint)nextOffset);
		}

		Debug.Fail("This point should be unreachable.");

	E_TooManyFields:
		E_TooManyFields(fldList.Count);

	E_TooManyClasses:
		E_TooManyClasses(clsSet.Count);
	}

	[DoesNotReturn]
	private void E_TooManyClasses(int currentCount) {
		Debug.Assert(currentCount > MaxClassCount);
		throw new InvalidOperationException(
			$"Total number of classes (currently {currentCount}) shouldn't exceed {MaxClassCount};" +
			$"{Environment.NewLine}Entity: {GetDebugLabel()};" +
			$"{Environment.NewLine}Schema: {_SchemaId};");
	}

	// --

	private const int SchemaUsumDigestLength = 30; // 240-bit hash

	private byte[] FinishWithSchemaUsum(ref Blake2bHashState hasher, byte fldLocalCount) {
		const int UsumVer = 1; // The version varint
		const int UsumVerLength = 1; // The varint length is a single byte for now
		Debug.Assert(VarInts.Length(UsumVer) == UsumVerLength);
		Debug.Assert(VarInts.Bytes(UsumVer)[0] == UsumVer);

		const int ExtraBytesNeeded = 1; // To encode `fldLocalCount`

		const int UsumPrefixLength = UsumVerLength + ExtraBytesNeeded;
		Span<byte> usum = stackalloc byte[UsumPrefixLength + SchemaUsumDigestLength];

		usum[0] = UsumVer; // Prepend version varint
		usum[1] = fldLocalCount;

		hasher.Finish(usum[UsumPrefixLength..]);

		// TODO In the future, once we're either using `sqlite3_stmt` directly or have replaced `Microsoft.Data.Sqlite`
		// with a custom version more suited to our needs, rent/stackalloc a buffer for the hash output instead, then
		// pass that as a `ReadOnlySpan<byte>` to `sqlite3_bind_blob()`.
		return usum.ToArray();
	}
}
