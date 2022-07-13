namespace Kokoro.Internal;
using Microsoft.Data.Sqlite;

partial class FieldedEntity {
	private const int MaxClassCount = byte.MaxValue + 1;

	private HashSet<long>? _AddedClasses;
	private HashSet<long>? _RemovedClasses;

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
