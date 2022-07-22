namespace Kokoro.Internal;
using Blake2Fast;
using Blake2Fast.Implementation;
using Kokoro.Common.Buffers;
using Kokoro.Common.Util;
using Kokoro.Internal.Sqlite;
using Microsoft.Data.Sqlite;
using System.Runtime.InteropServices;

partial class FieldedEntity {
	private const int MaxClassCount = byte.MaxValue;

	private HashSet<long>? _AddedClasses;
	private HashSet<long>? _RemovedClasses;

	// --

	[SuppressMessage("Style", "IDE1006:Naming Styles")]
	private static class SchemaRewrite {

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
						$"with rowid {a.rowid}");
				}
				{
					cmp = string.CompareOrdinal(a.name, b.name);
					Debug.Assert(cmp != 0, $"Impossible! Two fields have " +
						$"different rowids ({a.rowid} and {b.rowid}) but " +
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
	/// <para>
	/// Violation of the above contract may result in undefined behavior.
	/// </para>
	/// </remarks>
	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	[SkipLocalsInit]
	private protected void RewriteSchema(ref FieldsReader fr, int hotStoreLimit, ref FieldsWriter fw) {
		DAssert_FieldsWriterPriorRewrite(ref fw);

		var clsSet = _AddedClasses;
		clsSet = clsSet != null ? new(clsSet) : new();

		var remClsSet = _RemovedClasses;
		remClsSet = remClsSet != null ? new(remClsSet) : clsSet;

		// Get the old schema's direct classes
		var db = fr.Db;
		using (var cmd = db.CreateCommand()) {
			cmd.Set("SELECT cls FROM SchemaToDirectClass WHERE schema=$schema")
				.AddParams(new("$schema", _SchemaRowId));

			using var r = cmd.ExecuteReader();
			while (r.Read()) {
				r.DAssert_Name(0, "cls");
				long cls = r.GetInt64(0);

				if (!remClsSet.Contains(cls))
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
			foreach (var cls in clsSet) {
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

			Dictionary<StringKey, FieldVal>? fchanges = _FieldChanges;
			if (fchanges == null) goto NoFieldChanges;

			var fchanges_iter = fchanges.GetEnumerator();
			if (!fchanges_iter.MoveNext()) goto NoFieldChanges;

			fldMapOld = new(fchanges.Count);

			db.ReloadFieldNameCaches(); // Needed by `db.LoadStale…()` below
			do {
				var (fname, fval) = fchanges_iter.Current;
				long fld = db.LoadStaleOrEnsureFieldId(fname);

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
					.AddParams(new("$schema", _SchemaRowId));

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
		int fldColdCount = 0;

		using (var cmd = db.CreateCommand()) {
			SqliteParameter cmd_cls;
			cmd.Set(
				"SELECT\n" +
					"fld.rowid AS fld,\n" +
					"fld.name AS name,\n" +
					"cls2fld.ord AS ord,\n" +
					"cls2fld.sto AS sto\n" +
				"FROM ClassToField AS cls2fld,FieldName AS fld\n" +
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
							$"Two fields have the same rowid (which is {fld}) but different names:{Environment.NewLine}" +
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
								} else if ((FieldStoreTypeSInt)entry_sto >= 0) {
									fldColdCount--;
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
						} else if ((FieldStoreTypeSInt)sto >= 0) {
							fldColdCount++;
						}
					} else {
						fldSharedCount++;
					}

				LeaveEntry:
					;
				}
			}
		}

		if (fldList.Count > MaxFieldCount) goto E_TooManyFields;

		// The remaining old field mappings will become floating fields -- i.e.,
		// fields not defined by the (new) schema.
		{
			int fldMapOldCount = fldMapOld.Count;
			if (fldMapOldCount != 0) {
				var floFlds = fw._FloatingFields = new(fldMapOldCount);
				foreach (var (fld, entry) in fldMapOld) {
					floFlds.Add((fld, entry.FVal ?? fr.Read(entry.FSpec)));
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

			// -=-

			byte[] usum;

			using var renter_offsets = BufferRenter<int>
				.Create(fldListIdxs.Length, out var buffer_offsets);

			int sharedFValsSize;

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
				// 4. The number of shared fields, as a 32-bit integer.
				// 5. The field values of shared fields, each prepended with its
				// length.
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
				}

				// Hash the list of indirect class's `csum`
				{
				}

				// Hash the shared fields' values (length-prepend each), while
				// also gathering the shared fields' offsets.
				{
				}
			}

			// TODO Look up schema matching `usum`
		}

		// TODO-XXX Finish implementation

		return; // ---

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
			$"{Environment.NewLine}Schema: {_SchemaRowId};");
	}

	// --

	private const int SchemaUsumDigestLength = 30; // 240-bit hash

	private byte[] FinishWithSchemaUsum(ref Blake2bHashState hasher, byte fldLocalCount) {
		const int UsumVer = 1; // The version varint
		const int UsumVerLength = 1; // The varint length is a single byte for now
		Debug.Assert(VarInts.Length(UsumVer) == UsumVerLength);

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
