namespace Kokoro.Internal;
using Microsoft.Data.Sqlite;

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
	}

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
			using var inclCmd = db.Cmd("SELECT incl FROM ClassToInclude WHERE cls=$cls")
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
				foverrides.Add(fld, fval);
			} while (fchanges_iter.MoveNext());

			// --
			goto DoneWithFieldChanges;

		NoFieldChanges:
			foverrides = new();

		DoneWithFieldChanges:
			;
		}

		// TODO-XXX Finish implementation

		return; // ---

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
