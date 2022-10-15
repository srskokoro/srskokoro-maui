namespace Kokoro.Internal;
using Blake2Fast;
using Blake2Fast.Implementation;
using Kokoro.Common.Util;
using Kokoro.Internal.Sqlite;
using Microsoft.Data.Sqlite;
using System.Runtime.InteropServices;

partial class FieldedEntity {
	internal const int MaxClassCount = byte.MaxValue;

	private static class SchemaRewrite {

		internal struct ClassInfo {
			public long RowId;
			public byte[]? Csum;

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public ClassInfo(long RowId, byte[]? Csum) {
				this.RowId = RowId;
				this.Csum = Csum;
			}
		}

		internal struct FieldInfo {
			public long RowId;

			public int ClsOrd;
			public FieldStoreType Sto;
			public int Ord;

			public string Name;
#if DEBUG
			public long ClsRowId;
#endif

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public FieldInfo(
				long RowId,
				int ClsOrd, FieldStoreType Sto, int Ord,
				string Name
#if DEBUG
				, long ClsRowId
#endif
			) {
				this.RowId = RowId;

				this.ClsOrd = ClsOrd;
				this.Sto = Sto;
				this.Ord = Ord;

				this.Name = Name;
#if DEBUG
				this.ClsRowId = ClsRowId;
#endif
			}
		}

		internal static class Comparison_clsList {
			private static Comparison<ClassInfo>? _Inst;

			internal static Comparison<ClassInfo> Inst {
				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				get => _Inst ??= Impl;
			}

			[MethodImpl(MethodImplOptions.AggressiveOptimization)]
			[SkipLocalsInit]
			private static int Impl(ClassInfo a, ClassInfo b) {
				/// WARNING: This is used for sorting, and if the way things are
				/// sorted changes, the <see cref="SchemaUsumVer"/> version
				/// integer must either increment or change.
				///
				/// The way the elements are compared may still be changed
				/// (especially to optimize code), provided that the sorting
				/// doesn't change -- otherwise, the aforesaid version integer
				/// must increment or change.

				var x = a.Csum;
				var y = b.Csum;

				if (x == null) goto Null_x; // This becomes a conditional jump forward to not favor it
				if (y == null) goto NonNull_x_Null_y; // ^

				// See also, https://crypto.stackexchange.com/questions/54544/how-to-to-calculate-the-hash-of-an-unordered-set
				return x.AsDangerousROSpan().SequenceCompareTo(y.AsDangerousROSpan());

			Null_x:
				return y == null ? 0 : -1;
			NonNull_x_Null_y:
				return 1;
			}
		}

		internal static class Comparison_fldList {
			private static Comparison<FieldInfo>? _Inst;

			internal static Comparison<FieldInfo> Inst {
				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				get => _Inst ??= Impl;
			}

			[MethodImpl(MethodImplOptions.AggressiveOptimization)]
			[SkipLocalsInit]
			private static int Impl(FieldInfo a, FieldInfo b) {
				/// WARNING: This is used for sorting, and if the way things are
				/// sorted changes, the <see cref="SchemaUsumVer"/> version
				/// integer must either increment or change.
				///
				/// The way the elements are compared may still be changed
				/// (especially to optimize code), provided that the sorting
				/// doesn't change -- otherwise, the aforesaid version integer
				/// must increment or change.

				int cmp;
				{
					// Partition the sorted entries by field store type

					// NOTE: Using `Enum.CompareTo()` has a boxing cost, which
					// sadly, JIT doesn't optimize out (for now). So we must
					// cast the enums to their int counterparts to avoid the
					// unnecessary box.
					var aSto = (FieldStoreTypeInt)a.Sto;
					var bSto = (FieldStoreTypeInt)b.Sto;

					cmp = aSto.CompareTo(bSto);
					if (cmp != 0) goto Return;
				}
				{
					cmp = a.ClsOrd.CompareTo(b.ClsOrd);
					if (cmp != 0) goto Return;
				}
				{
					cmp = a.Ord.CompareTo(b.Ord);
					if (cmp != 0) goto Return;
				}
				{
					Debug.Assert(a.RowId != b.RowId, $"Expecting no " +
						$"duplicates but found a duplicate field entry with " +
						$"name id {a.RowId}");
				}
				{
					cmp = string.CompareOrdinal(a.Name, b.Name);
					Debug.Assert(cmp != 0, $"Impossible! Two fields have " +
						$"different name ids ({a.RowId} and {b.RowId}) but " +
						$"same name: {a.Name}");
				}
			Return:
				return cmp;
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

		List<SchemaRewrite.ClassInfo> clsList = new(dclsCount);

		foreach (long rowid in clsSet) {
			clsList.Add(new(RowId: rowid, Csum: null));
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
							clsList.Add(new(RowId: incl, Csum: null));
						}
					}
				}
			}
		}

		// Partition the list of classes into two, direct and indirect classes,
		// then sort each partition to a predictable order.
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
			/// WARNING: The expected order of inputs to be fed to the above
			/// hasher must be strictly as follows:
			///
			/// 0. The number of direct classes, as a 32-bit integer, in little-
			/// endian format.
			/// 1. The `csum`s from the list of direct classes (with the list of
			/// classes sorted by <see cref="SchemaRewrite.Comparison_clsList.Inst"/>
			/// beforehand).
			/// 2. The `csum`s from the list of indirect classes (with the list
			/// of classes sorted by <see cref="SchemaRewrite.Comparison_clsList.Inst"/>
			/// beforehand).
			///   - WARNING: Notice that, this last entry is variable-sized but
			///   not length-prepended. There is no clear termination.
			///
			/// Unless stated otherwise, all integer inputs should be consumed
			/// in their little-endian form. <see href="https://en.wikipedia.org/wiki/Endianness"/>
			///
			/// The <see cref="SchemaUsumVer"/> version integer must increment
			/// or change, should any of the following happens:
			///
			/// - The resulting hash BLOB length changes.
			/// - The algorithm for the resulting hash BLOB changes.
			/// - An input entry (from the list of inputs above) was removed.
			/// - The order of an input entry (from the list of inputs above)
			/// was changed or shifted.
			/// - An input entry's size (in bytes) changed while it's expected
			/// to be fixed-sized (e.g., not length-prepended).
			///
			/// The aforesaid version integer needs not to change if further
			/// input entries were to be appended (from the list of inputs
			/// above), provided that the last input entry has a clear
			/// termination, i.e., fixed-sized or length-prepended.

			// --

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
							r.DAssert_Name(0, "idx_e_sto");
							FieldSpec fspec2 = r.GetInt32(0);
							fspec2.DAssert_Valid();

							// NOTE: Should resolve field enum value, if any.
							fval = fr.Read(fspec2);
							Debug.Assert(fval.TypeHint != FieldTypeHint.Enum);
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
						$"SELECT idx_e_sto FROM {Prot.SchemaToField}\n" +
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
			cmd.Set($"SELECT fld,idx_e_sto FROM {Prot.SchemaToField} WHERE schema=$schema")
				.AddParams(new("$schema", oldSchemaId));

			using var r = cmd.ExecuteReader();
			while (r.Read()) {
				r.DAssert_Name(0, "fld");
				long fld = r.GetInt64(0);

				ref var entry = ref CollectionsMarshal.GetValueRefOrAddDefault(
					fldMapOld, key: fld, out _
				);
				if (entry == null) {
					r.DAssert_Name(1, "idx_e_sto");
					FieldSpec fspec = r.GetInt32(1);
					fspec.DAssert_Valid();

					// NOTE: Should resolve field enum value, if any.
					entry = fr.Read(fspec);
					Debug.Assert(entry.TypeHint != FieldTypeHint.Enum);
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
			/// WARNING: The expected order of inputs to be fed to the above
			/// hasher must be strictly as follows:
			///
			/// 0. The bare schema `usum`.
			/// 1. The result of each shared field's <see cref="FieldVal.FeedTo(ref Blake2bHashState)"/>
			/// method, including those with null field values, provided that,
			/// the aforesaid method treats each shared field data as several
			/// inputs composed of the following, in strict order:
			///   1.1. The type hint, as a 32-bit integer.
			///   1.2. If not a null field value (indicated by the type hint),
			///   then the data bytes with its (32-bit) length prepended.
			///     - NOTE: The length said here is the length of the data
			///     bytes, excluding the type hint.
			///
			///   - WARNING: There can be zero or more shared fields, and this
			///   last entry is variable-sized but not length-prepended. There
			///   is no clear termination.
			///
			///   - The shared fields must be fed to the hasher in the order of
			///   their indices.
			///
			///   - Any field enum value should be resolved, so that the actual
			///   field value is the one that is hashed.
			///
			/// Unless stated otherwise, all integer inputs should be consumed
			/// in their little-endian form. <see href="https://en.wikipedia.org/wiki/Endianness"/>
			///
			/// The <see cref="SchemaUsumVer"/> version integer must increment
			/// or change, should any of the following happens:
			///
			/// - The resulting hash BLOB length changes.
			/// - The algorithm for the resulting hash BLOB changes.
			/// - An input entry (from the list of inputs above) was removed.
			/// - The order of an input entry (from the list of inputs above)
			/// was changed or shifted.
			/// - An input entry's size (in bytes) changed while it's expected
			/// to be fixed-sized (e.g., not length-prepended).
			///
			/// The aforesaid version integer needs not to change if further
			/// input entries were to be appended (from the list of inputs
			/// above), provided that the last input entry has a clear
			/// termination, i.e., fixed-sized or length-prepended.

			// --

			using (var cmd = db.CreateCommand()) {
				cmd.Set(
					$"SELECT idx,fld FROM {Prot.SchemaToField}\n" +
					$"WHERE schema=$schema AND loc=0\n" +
					$"ORDER BY idx" // Needed also to force usage of DB index
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

							// NOTE: All field enum values should've been
							// resolved by now, unless the old schema doesn't
							// have the needed field enum or the `FieldVal` came
							// from either a mischievous floating field or a
							// noncompliant field change input.

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
		{
			long newSchemaId;
			using (var cmd = db.CreateCommand()) {
				cmd.Set($"SELECT rowid FROM {Prot.Schema} WHERE usum=$usum")
					.AddParams(new("$usum", schemaUsum));

				using var r = cmd.ExecuteReader();
				if (r.Read()) {
					r.DAssert_Name(0, "rowid");
					newSchemaId = r.GetInt64(0);
				} else {
					newSchemaId = 0;
				}
			}
			// NOTE: Must also ensure that the schema rowid is never zero, even
			// if the rowid came from the DB.
			if (newSchemaId != 0) {
				schemaId = newSchemaId;
				goto SchemaResolved;
			}
		}
		goto InitNonBareSchema;
	InitNonBareSchema_Done:
		Debug.Assert(schemaId != 0);

	SchemaResolved:
		DInit_StoreLengthsAndFDescs(ref fw);

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
						/// <seealso cref="FieldsWriter.LoadOffsets"/>

						Debug.Assert((uint)i < (uint)fw._Entries.Length);
						// TODO If `fspec` indicates an enum group, properly set `fval` to an enum elem `fval`
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
		schemaId = InitBareSchema(db, clsList, dclsCount, schemaUsum);
		goto InitBareSchema_Done;

	InitNonBareSchema:
		schemaId = InitNonBareSchema(db,
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
	private static long InitBareSchema(KokoroSqliteDb db, List<SchemaRewrite.ClassInfo> clsList, int dclsCount, byte[] usum) {
		DAssert_BareSchemaUsum(usum);

		Debug.Assert(db.Context != null);
		long schemaId = db.Context.NextSchemaId();

		List<SchemaRewrite.FieldInfo> fldList = new();

		int fldSharedCount = 0;
		int fldHotCount = 0;

		SqliteParameter cmd_schema = new("$schema", schemaId);
		SqliteParameter cmd_rowid = new() { ParameterName = "$rowid" };

		ref var cls_r0 = ref clsList.AsSpan().DangerousGetReference();
		int clsCount = clsList.Count;
		if (clsCount != 0) {
			Debug.Assert(clsCount > 0);
			Debug.Assert(clsCount >= dclsCount);

			// Used to spot duplicate entries
			Dictionary<long, int> fldMap = new();

			using var fldInfoCmd = db.CreateCommand();
			fldInfoCmd.Set(
				$"SELECT\n" +
					$"fld.rowid fld,\n" +
					$"fld.name name,\n" +
					$"cls2fld.ord ord,\n" +
					$"cls2fld.sto sto,\n" +
					$"cls.ord clsOrd\n" +
				$"FROM {Prot.Class} cls,{Prot.ClassToField} cls2fld,{Prot.NameId} fld\n" +
				$"WHERE cls.rowid=$rowid AND cls2fld.cls=cls.rowid AND fld.rowid=cls2fld.fld"
			).AddParams(cmd_rowid);

			using var insClsCmd = db.CreateCommand();
			SqliteParameter insClsCmd_csum, insClsCmd_ind;
			insClsCmd.Set(
				$"INSERT INTO {Prot.SchemaToClass}" +
				$"(schema,cls,csum,ind)" +
				$"\nVALUES" +
				$"($schema,$rowid,$csum,$ind)"
			).AddParams(
				cmd_schema, cmd_rowid,
				insClsCmd_csum = new() { ParameterName = "$csum" },
				insClsCmd_ind = new() { ParameterName = "$ind" }
			);

			int c = 0;
			do {
				ref var cls = ref U.Add(ref cls_r0, c);

				// --
				{
					byte[]? csum = cls.Csum;

					// Skips nonexistent classes (indicated by null `csum`)
					if (csum == null) continue;

					insClsCmd_csum.Value = csum;
					insClsCmd_ind.Value = c >= dclsCount;
					cmd_rowid.Value = cls.RowId;

					int updated = insClsCmd.ExecuteNonQuery();
					Debug.Assert(updated == 1, $"Updated: {updated}");
				}

				using var r = fldInfoCmd.ExecuteReader();
				while (r.Read()) {
					r.DAssert_Name(0, "fld");
					long fldId = r.GetInt64(0);

					r.DAssert_Name(1, "name");
					string name = r.GetString(1);

					r.DAssert_Name(2, "ord");
					int ord = r.GetInt32(2);

					r.DAssert_Name(3, "sto");
					FieldStoreType sto = (FieldStoreType)r.GetByte(3);
					sto.DAssert_Defined();

					r.DAssert_Name(4, "clsOrd");
					int clsOrd = r.GetInt32(4);

					ref int i = ref CollectionsMarshal.GetValueRefOrAddDefault(
						fldMap, key: fldId, out bool exists
					);
					if (!exists) {
						i = fldList.Count; // The new entry's index in the list

						fldList.Add(new(
							RowId: fldId,
							ClsOrd: clsOrd, Sto: sto, Ord: ord,
							Name: name
#if DEBUG
							, ClsRowId: cls.RowId
#endif
						));
					} else {
						// Get a `ref` to the already existing entry
						ref var fld = ref fldList.AsSpan().DangerousGetReferenceAt(i);

						Debug.Assert(fldId == fld.RowId);
						Debug.Assert(name == fld.Name, $"Impossible! " +
							$"Two fields have the same name ID (which is {fldId}) but different names:" +
							$"{Environment.NewLine}Name of 1st instance: {name}" +
							$"{Environment.NewLine}Name of 2nd instance: {fld.Name}");

						// Attempt to replace existing entry
						// --
						{
							var a = clsOrd; var b = fld.ClsOrd;
							if (a < b) goto ReplaceEntry;
							if (a > b) goto LeaveEntry;
						}
						{
							var a = sto; var b = fld.Sto;
							if (a < b) goto ReplaceEntry;
							if (a > b) goto LeaveEntry;
						}
						{
							var a = ord; var b = fld.Ord;
							if (a < b) goto ReplaceEntry;
							//if (a > b) goto LeaveEntry;
						}
						{
#if DEBUG
							Debug.Assert(cls.RowId != fld.ClsRowId,
								$"Unexpected! Duplicate rows returned for class-and-field pair:" +
								$"{Environment.NewLine}Class ID: {cls.RowId}" +
								$"{Environment.NewLine}Field Name: {name}" +
								$"{Environment.NewLine}Field ID: {fldId}");
#endif
							// Same entry or practically the same
							goto LeaveEntry;
						}

					ReplaceEntry:
						{
							var oldSto = fld.Sto;
							if (oldSto != FieldStoreType.Shared) {
								if (oldSto == FieldStoreType.Hot) {
									fldHotCount--;
								}
							} else {
								fldSharedCount--;
							}
						}
						fld = new(
							RowId: fldId,
							ClsOrd: clsOrd, Sto: sto, Ord: ord,
							Name: name
#if DEBUG
							, ClsRowId: cls.RowId
#endif
						);
					}

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
			} while (++c < clsCount);
		}

		int fldCount = fldList.Count;
		Debug.Assert(fldCount >= 0);
		Debug.Assert((uint)fldSharedCount <= (uint)fldCount);
		Debug.Assert((uint)fldHotCount <= (uint)fldCount);
		Debug.Assert((uint)(fldSharedCount + fldHotCount) <= (uint)fldCount);

		int fldLocalCount = fldCount - fldSharedCount;
		Debug.Assert((uint)fldHotCount <= (uint)fldLocalCount);

		if (fldCount > MaxFieldCount) goto E_TooManyFields;
		if (fldCount != 0) {
			fldList.Sort(SchemaRewrite.Comparison_fldList.Inst);

			using var cmd = db.CreateCommand();
			SqliteParameter cmd_idx_e_sto;
			cmd.Set(
				$"INSERT INTO {Prot.SchemaToField}" +
				$"(schema,fld,idx_e_sto)" +
				$"\nVALUES" +
				$"($schema,$rowid,$idx_e_sto)"
			).AddParams(
				cmd_schema, cmd_rowid,
				cmd_idx_e_sto = new() { ParameterName = "$idx_e_sto" }
			);

			ref var fld_r0 = ref fldList.AsSpan().DangerousGetReference();

			if (fldSharedCount != 0) {
				SaveFieldInfos(
					cmd,
					cmd_fld: cmd_rowid,
					cmd_idx_e_sto: cmd_idx_e_sto,

					ref fld_r0,
					start: 0, end: fldSharedCount,
					FieldStoreType.Shared
				);
			}

			fld_r0 = ref U.Add(ref fld_r0, fldSharedCount);

			if (fldHotCount != 0) {
				SaveFieldInfos(
					cmd,
					cmd_fld: cmd_rowid,
					cmd_idx_e_sto: cmd_idx_e_sto,

					ref fld_r0,
					start: 0, end: fldHotCount,
					FieldStoreType.Hot
				);
			}

			if (fldHotCount < fldLocalCount) {
				SaveFieldInfos(
					cmd,
					cmd_fld: cmd_rowid,
					cmd_idx_e_sto: cmd_idx_e_sto,

					ref fld_r0,
					start: fldHotCount, end: fldLocalCount,
					FieldStoreType.Cold
				);
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			[SkipLocalsInit]
			static void SaveFieldInfos(
				SqliteCommand cmd,
				SqliteParameter cmd_fld,
				SqliteParameter cmd_idx_e_sto,

				ref SchemaRewrite.FieldInfo fld_r0,

				int start, int end,
				FieldStoreType storeType
			) {
				Debug.Assert((uint)start < (uint)end);
				Debug.Assert((uint)end
					<= (uint)MaxFieldCount && (uint)MaxFieldCount
					<= (uint)FieldSpec.MaxIndex);

				FieldSpec idx_e_sto_c = new(start, storeType);
				FieldSpec idx_e_sto_n = new(end, storeType);

				do {
					cmd_fld.Value = U.Add(ref fld_r0, idx_e_sto_c.Index).RowId;
					cmd_idx_e_sto.Value = idx_e_sto_c.Int;

					int updated = cmd.ExecuteNonQuery();
					Debug.Assert(updated == 1, $"Updated: {updated}");
				} while ((
					idx_e_sto_c += FieldSpec.IndexIncrement
				).Value < idx_e_sto_n.Value);
			}
		}

		using (var cmd = db.CreateCommand()) {
			const string DataWithNoSharedFieldVals = "x'00'"; // An SQLite BLOB literal
			VarInts.DAssert_Equals(stackalloc byte[FieldsDesc.VarIntLengthForEmpty] { 0x00 }, FieldsDesc.Empty);

			cmd.Set(
				$"INSERT INTO {Prot.Schema}" +
				$"(rowid,usum,hotCount,coldCount,sharedCount,bareSchema,data)" +
				$"\nVALUES" +
				$"($schema,$usum,$hotCount,$coldCount,$sharedCount,$schema,{DataWithNoSharedFieldVals})"
			).AddParams(
				cmd_schema,
				new("$usum", usum),
				new("$hotCount", fldHotCount),
				new("$coldCount", fldLocalCount - fldHotCount),
				new("$sharedCount", fldSharedCount)
			);

			int updated = cmd.ExecuteNonQuery();
			Debug.Assert(updated == 1, $"Updated: {updated}");
		}

	// -=-

	Done:
		return schemaId; // ---

	E_TooManyFields:
		db.Context?.UndoSchemaId(schemaId);
		E_TooManyFields(fldCount);

		// -=-

		Debug.Fail("This point should be unreachable.");
		goto Done;
	}

	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	[SkipLocalsInit]
	private static long InitNonBareSchema(KokoroSqliteDb db, long bareSchemaId, byte[] nonBareUsum, ref FieldsWriter fw, int nsc) {
		Debug.Assert(nsc > 0);
		Debug.Assert(nsc <= fw._Offsets?.Length); // `false` on null array
		Debug.Assert(nsc <= fw._Entries?.Length);
		DAssert_NonBareSchemaUsum(nonBareUsum);

		Debug.Assert(db.Context != null);
		long schemaId = db.Context.NextSchemaId();

		SqliteParameter cmd_schema = new("$schema", schemaId);
		SqliteParameter cmd_bareSchema = new("$bareSchema", bareSchemaId);

		using (var cmd = db.CreateCommand()) {
			const string Table = Prot.SchemaToClass;
			const string Vals = "$schema";
			const string ValCols = "schema";
			const string Cols = "cls,csum,ind";

			cmd.Set(
				$"INSERT INTO {Table}({Cols},{ValCols})\n" +
				$"SELECT {Cols},{Vals} FROM {Table}\n" +
				$"WHERE schema=$bareSchema"
			).AddParams(
				cmd_schema,
				cmd_bareSchema
			);

			cmd.ExecuteNonQuery();
		}

		using (var cmd = db.CreateCommand()) {
			const string Table = Prot.SchemaToEnum;
			const string Vals = "$schema";
			const string ValCols = "schema";
			const string Cols = "idx_e,type,data";

			cmd.Set(
				$"INSERT INTO {Table}({Cols},{ValCols})\n" +
				$"SELECT {Cols},{Vals} FROM {Table}\n" +
				$"WHERE schema=$bareSchema"
			).AddParams(
				cmd_schema,
				cmd_bareSchema
			);

			cmd.ExecuteNonQuery();
		}

		// Copy local field specs
		using (var cmd = db.CreateCommand()) {
			const string Table = Prot.SchemaToField;
			const string Vals = "$schema";
			const string ValCols = "schema";
			const string Cols = "fld,idx_e_sto";

			cmd.Set(
				$"INSERT INTO {Table}({Cols},{ValCols})\n" +
				$"SELECT {Cols},{Vals} FROM {Table}\n" +
				$"WHERE schema=$bareSchema AND loc=1"
			).AddParams(
				cmd_schema,
				cmd_bareSchema
			);

			cmd.ExecuteNonQuery();
		}

		// Copy shared field specs, while also resolving any field enum indices
		using (var cmd = db.CreateCommand()) {
			const string Table = Prot.SchemaToField;
			const string Vals = "$schema";
			const string ValCols = "schema";
			const string Cols = "fld,idx_e_sto";

			cmd.Set(
				$"INSERT INTO {Table}({Cols},{ValCols})\n" +
				$"SELECT {Cols},{Vals} FROM {Table}\n" +
				$"WHERE schema=$bareSchema AND loc=0\n" +
				$"RETURNING idx_e_sto"
			).AddParams(
				cmd_schema,
				cmd_bareSchema
			);

			using var r = cmd.ExecuteReader();
			while (r.Read()) {
				r.DAssert_Name(0, "idx_e_sto");
				FieldSpec fspec = r.GetInt32(0);
				Debug.Assert(fspec.StoreType == FieldStoreType.Shared, "Should only do this for shared fields");

				int enumGroup = fspec.EnumGroup;
				if (enumGroup != 0) {
					int i = fspec.Index;

					ref var entry = ref fw._Entries.DangerousGetReferenceAt(i);
					Debug.Assert(entry != null, $"Unexpected null entry at {i}");

					entry = ResolveFieldEnumIndex(db, schemaId, enumGroup, entry);
				}
			}
		}

		byte[] sharedData;
		{
			int sharedFValsSize;
			try {
				sharedFValsSize = fw.LoadOffsets(nextOffset: 0, start: 0, end: nsc);
			} catch (Exception) {
				db.Context?.UndoSchemaId(schemaId);
				throw;
			}

			ref int offsets_r0 = ref fw._Offsets.DangerousGetReference();

			int sharedFOffsetSizeM1Or0 = (
				(uint)U.Add(ref offsets_r0, nsc-1)
			).CountBytesNeededM1Or0();

			FieldsDesc sharedFDesc = new(
				fCount: nsc,
				fOffsetSizeM1Or0: sharedFOffsetSizeM1Or0
			);

			Span<byte> buffer_sharedFDesc = stackalloc byte[VarInts.MaxLength32];
			buffer_sharedFDesc = buffer_sharedFDesc.Slice(0, VarInts.Write(buffer_sharedFDesc, sharedFDesc));

			int sharedFOffsetSize = sharedFOffsetSizeM1Or0 + 1;

			// NOTE: The first offset value is never stored, as it'll always be
			// zero otherwise.
			int sharedStoreLength = buffer_sharedFDesc.Length
				+ (nsc - 1) * sharedFOffsetSize
				+ sharedFValsSize;

			sharedData = GC.AllocateUninitializedArray<byte>(sharedStoreLength);
			MemoryStream destination = new(sharedData);

			destination.Write(buffer_sharedFDesc);

			// Write the field offsets
			{
				// NOTE: The first offset value is never stored, as it'll always
				// be zero otherwise.
				Debug.Assert(offsets_r0 == 0);

				// Skips to the second offset value (at index 1)
				for (int i = 1; i < nsc; i++) {
					destination.WriteUInt32AsUIntXLE(
						(uint)U.Add(ref offsets_r0, i),
						sharedFOffsetSize);
				}
			}

			// Write the field values
			{
				ref var entries_r0 = ref fw._Entries.DangerousGetReference();
				int i = 0;
				do {
					FieldVal? fval = U.Add(ref entries_r0, i);
					Debug.Assert(fval != null, $"Unexpected null entry at {i}");
					fval.WriteTo(destination);
				} while (++i < nsc);
			}
		}

		using (var cmd = db.CreateCommand()) {
			const string Table = Prot.Schema;
			const string Vals = "$schema,$bareSchema,$usum,$data";
			const string ValCols = "rowid,bareSchema,usum,data";
			const string Cols = "hotCount,coldCount,sharedCount";

			cmd.Set(
				$"INSERT INTO {Table}({Cols},{ValCols})\n" +
				$"SELECT {Cols},{Vals} FROM {Table}\n" +
				$"WHERE rowid=$bareSchema"
			).AddParams(
				cmd_schema,
				cmd_bareSchema,
				new("$usum", nonBareUsum),
				new("$data", sharedData)
			);

			int updated = cmd.ExecuteNonQuery();
			Debug.Assert(updated == 1, $"Updated: {updated}");
		}

		// Done!
		return schemaId;
	}

	private const int SchemaUsumLength = SchemaUsumVerLength + SchemaUsumDigestLength;

	// The version varint
	private const int SchemaUsumVer = 1;
	// The varint length is a single byte for now
	private const int SchemaUsumVerLength = 1;

	private const int SchemaUsumDigestLength = 31; // 248-bit hash

	private static byte[] FinishWithSchemaUsum(ref Blake2bHashState hasher, bool hasSharedData) {
		VarInts.DAssert_Equals(stackalloc byte[SchemaUsumVerLength] { SchemaUsumVer }, SchemaUsumVer);

		Debug.Assert(SchemaUsumLength == SchemaUsumVerLength + SchemaUsumDigestLength);
		Span<byte> usum = stackalloc byte[SchemaUsumLength];
		hasher.Finish(usum.Slice(SchemaUsumVerLength));

		// Prepend version varint
		usum[0] = SchemaUsumVer;
		// Apply desired "has shared data" bit flag
		usum[1] = (byte)((usum[1] & unchecked((sbyte)0xFE)) | hasSharedData.ToByte());

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
		Debug.Assert(usum.Length == SchemaUsumLength);

		// Expect version 1 (since the check after is valid only for that)
		Debug.Assert(usum[0] == 1, $"Bytes: {Convert.ToHexString(usum)}");
		// Expect that the "has shared data" bit flag matches
		Debug.Assert((usum[1] & 1) == hasSharedData.ToByte(), $"Bytes: {Convert.ToHexString(usum)}");
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

	[DoesNotReturn]
	private static void E_TooManyFields(int count) {
		Debug.Assert(count > MaxFieldCount);
		throw new InvalidOperationException(
			$"Total number of fields (currently {count}) shouldn't exceed {MaxFieldCount}.");
	}

	[DoesNotReturn]
	private static void E_TooManyClasses(int count) {
		Debug.Assert(count > MaxClassCount);
		throw new InvalidOperationException(
			$"Total number of classes (currently {count}) shouldn't exceed {MaxClassCount}.");
	}
}
