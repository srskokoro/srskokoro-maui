namespace Kokoro.Internal;
using Kokoro.Common.Buffers;
using Kokoro.Internal.Sqlite;
using Microsoft.Data.Sqlite;
using System.Runtime.InteropServices;

partial class FieldedEntity {
	private const int MaxClassCount = byte.MaxValue + 1;

	private HashSet<long>? _AddedClasses;
	private HashSet<long>? _RemovedClasses;

	// --

	[SuppressMessage("Style", "IDE1006:Naming Styles")]
	private static class SchemaRewrite {

		internal record struct ClassInfo(
			long rowid, UniqueId uid, byte[] csum, int ord
		);

		internal record struct FieldInfo(
			long rowid,
			int cls_ord, FieldStoreType sto, int ord, FieldSpec old_idx_a_sto,
			long atarg, string name, FieldVal? new_fval, UniqueId cls_uid
		);
	}

	/// <remarks>
	/// CONTRACT: Must be called while inside a transaction (ideally, using <see cref="NestingWriteTransaction"/>).
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

		// Convert the field changes into pairs of field id and value
		// --

		Dictionary<long, FieldVal> foverrides;
		{
			Dictionary<StringKey, FieldVal>? fchanges = _FieldChanges;
			if (fchanges == null) goto NoFieldChanges;

			var fchanges_iter = fchanges.GetEnumerator();
			if (!fchanges_iter.MoveNext()) goto NoFieldChanges;

			foverrides = new(fchanges.Count);

			db.ReloadFieldNameCaches(); // Needed by `db.LoadStale…()` below
			do {
				var (fname, fval) = fchanges_iter.Current;
				long fld = db.LoadStaleOrEnsureFieldId(fname);

				bool added = foverrides.TryAdd(fld, fval);
				Debug.Assert(added, $"Shouldn't happen if running in a " +
					$"`{nameof(NestingWriteTransaction)}` or equivalent");
			} while (fchanges_iter.MoveNext());

			Debug.Assert(foverrides.Count == fchanges.Count);

			// --
			goto DoneWithFieldChanges;

		NoFieldChanges:
			foverrides = new();

		DoneWithFieldChanges:
			;
		}

		// Gather the fields associated with each class, skipping duplicates,
		// while also fetching any needed info
		// --

		List<SchemaRewrite.FieldInfo> fldList = new();

		int fldSharedCount = 0;
		int fldHotCount = 0;
		int fldColdCount = 0;

		// Used to spot duplicate entries and to resolve field alias targets
		Dictionary<long, int> fldMap = new();

		using (var cmd = db.CreateCommand()) {
			SqliteParameter cmd_cls;
			cmd.Set(
				"SELECT\n" +
					"fld.rowid AS fld,\n" +
					"fld.name AS name,\n" +
					"cls2fld.ord AS ord,\n" +
					"ifnull(cls2fld.sto,-1) AS sto,\n" +
					"ifnull(cls2fld.atarg,0) AS atarg,\n" +
					"ifnull(sch2fld.idx_a_sto,-1) AS old_idx_a_sto\n" +
				"FROM ClassToField AS cls2fld,FieldName AS fld LEFT JOIN SchemaToField AS sch2fld\n" +
					"ON sch2fld.schema=$oldSchema AND sch2fld.fld=cls2fld.fld\n" +
				"WHERE cls2fld.cls=$cls AND fld.rowid=cls2fld.fld"
			).AddParams(
				new("$oldSchema", _SchemaRowId),
				cmd_cls = new() { ParameterName = "$cls" }
			);

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

					r.DAssert_Name(4, "atarg");
					long atarg = r.GetInt64(4);

					r.DAssert_Name(5, "old_idx_a_sto");
					FieldSpec old_idx_a_sto = r.GetInt32(5);

					ref int i = ref CollectionsMarshal.GetValueRefOrAddDefault(fldMap, fld, out bool exists);
					if (!exists) {
						if (!foverrides.Remove(fld, out var new_fval) && (int)old_idx_a_sto < 0) {
							// Case: Field not defined by the old schema
							new_fval = OnLoadFloatingField(db, fld) ?? FieldVal.Null;
						}

						i = fldList.Count; // The new entry's index in the list

						// Add the new entry
						fldList.Add(new(
							rowid: fld,
							cls_ord: cls.ord, sto, ord, old_idx_a_sto,
							atarg, name, new_fval, cls_uid: cls.uid
						));
					} else {
						// Get a `ref` to the already existing entry
						ref var entry = ref fldList.AsSpan().DangerousGetReferenceAt(i);

						Debug.Assert(name == entry.name, $"Impossible! " +
							$"Two fields have the same rowid (which is {fld}) but different names:{Environment.NewLine}" +
							$"Name of 1st instance: {name}{Environment.NewLine}" +
							$"Name of 2nd instance: {entry.name}");

						Debug.Assert(old_idx_a_sto == entry.old_idx_a_sto, $"Impossible! " +
							$"Two fields have the same rowid (which is {fld}) and same old schema (which is {_SchemaRowId})" +
							$" but different `old_idx_a_sto` value:{Environment.NewLine}" +
							$"Value of 1st instance: {old_idx_a_sto}{Environment.NewLine}" +
							$"Value of 2nd instance: {entry.old_idx_a_sto}");

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
						// `atarg` may be different at this point: the class
						// with the lowest UID gets to define `atarg`
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
								} else if ((FieldStoreTypeInt)entry_sto >= 0) {
									fldColdCount--;
								}
							} else {
								fldSharedCount--;
							}
						}
						entry = new(
							rowid: fld,
							cls_ord: cls.ord, sto, ord, old_idx_a_sto,
							atarg, name, entry.new_fval, cls_uid: cls.uid
						);
					}

					Debug.Assert(typeof(FieldStoreTypeInt) == typeof(int));

					if (sto != FieldStoreType.Shared) {
						if (sto == FieldStoreType.Hot) {
							fldHotCount++;
						} else if ((FieldStoreTypeInt)sto >= 0) {
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

		// The remaining field changes are floating fields -- i.e., fields not
		// defined by the schema.
		if (foverrides.Count != 0) {
			fw._FloatingFields = foverrides
				.Select(entry => (entry.Key, entry.Value))
				.ToList();
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
			}

			using var renter_fldListIdxs = BufferRenter<byte>
				.CreateSpan(fldList.Count, out var fldListIdxs);
			InitWithIndices(fldListIdxs);

			using var renter_clsListIdxs = BufferRenter<byte>
				.CreateSpan(clsList.Count, out var clsListIdxs);
			InitWithIndices(clsListIdxs);

			// Sort the gathered lists by sorting their indices instead
			{
				// Sort the list of fields
				var fldList_capture = fldList;
				fldListIdxs.Sort(int (byte x, byte y) => {
					ref var r0 = ref fldList_capture.AsSpan().DangerousGetReference();
					ref var a = ref U.Add(ref r0, x);
					ref var b = ref U.Add(ref r0, y);

					int cmp;
					// Partition the sorted array by field store type
					{
						// NOTE: Using `Enum.CompareTo()` has a boxing cost,
						// which sadly, JIT doesn't optimize out (for now). So
						// we must cast the enums to their int counterparts to
						// avoid the unnecessary box.
						cmp = ((FieldStoreTypeInt)a.sto).CompareTo((FieldStoreTypeInt)b.sto);
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
				});

				// Partition the list of classes into two, direct and indirect
				// classes, then sort each partition separately.
				// --

				var clsList_capture = clsList;

				int clsList_comparison(byte x, byte y) {
					ref var r0 = ref clsList_capture.AsSpan().DangerousGetReference();
					return U.Add(ref r0, x).uid.CompareTo(U.Add(ref r0, y).uid);
				}

				// Sort the list of direct classes
				clsListIdxs[..dclsCount].Sort(clsList_comparison);

				// Sort the list of indirect classes
				clsListIdxs[dclsCount..].Sort(clsList_comparison);
			}
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
}
