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
		var clsSet = _AddedClasses;
		clsSet = clsSet != null ? new(clsSet) : new();

		var remClsSet = _RemovedClasses;
		remClsSet = remClsSet != null ? new(remClsSet) : clsSet;

		var db = fr.Db;

		// Get the old schema's direct classes
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
		if (dclsCount > MaxClassCount) E_TooManyClasses(dclsCount);
		List<SchemaRewrite.ClassInfo> clsList = new(dclsCount);

		// Get the needed info for each class
		using (var clsCmd = db.CreateCommand()) {
			SqliteParameter clsCmd_rowid;
			clsCmd.Set("SELECT uid,csum,ord FROM Class WHERE rowid=$rowid")
				.AddParams(clsCmd_rowid = new() { ParameterName = "$rowid" });

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
		}

		// TODO-XXX Finish implementation
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
