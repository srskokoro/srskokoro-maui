namespace Kokoro;
using Kokoro.Internal.Sqlite;

partial class KokoroContext {

	#region Operable Resources

	#region Next RowIDs

	// We manage the available rowids directly (rather than let SQLite manage it
	// for us) to ensure that no rowid belonging to an in-memory representation
	// of an entity will ever bump into a deleted-then-reinserted rowid now
	// belonging to a different entity -- also includes indirect deletions due
	// to rollbacks of transactions (or savepoints) undoing insertions.

	private void LoadNextRowIdsFrom(KokoroSqliteDb db) {
		using var cmd = db.CreateCommand("""
			SELECT ifnull(max(rowid), 0) FROM Items
			UNION ALL
			SELECT ifnull(max(rowid), 0) FROM Schemas
			UNION ALL
			SELECT ifnull(max(rowid), 0) FROM SchemaTypes
			UNION ALL
			SELECT ifnull(max(rowid), 0) FROM FieldNames
			""");

		using var reader = cmd.ExecuteReader();
		reader.Read(); _NextItemRowId = reader.GetInt64(0);
		reader.Read(); _NextSchemaRowId = reader.GetInt64(0);
		reader.Read(); _NextSchemaTypeRowId = reader.GetInt64(0);
		reader.Read(); _NextFieldNameRowId = reader.GetInt64(0);
	}

	private long _NextItemRowId;
	private long _NextSchemaRowId;
	private long _NextSchemaTypeRowId;
	private long _NextFieldNameRowId;

	internal long NextItemRowId() => NextRowId(ref _NextItemRowId);
	internal long NextSchemaRowId() => NextRowId(ref _NextSchemaRowId);
	internal long NextSchemaTypeRowId() => NextRowId(ref _NextSchemaTypeRowId);
	internal long NextFieldNameRowId() => NextRowId(ref _NextFieldNameRowId);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	private static long NextRowId(ref long nextRowIdFieldRef) {
		// The following trick won't work if the user somehow ran this with 2^63
		// threads, either in parallel or with threads suspended at unfortunate
		// moments. That should be deemed impossible due to resource limits, but
		// who knows? Perhaps some high-tech futuristic civilization knows… :P
		if (Interlocked.Increment(ref nextRowIdFieldRef) < 0) {
			Interlocked.Decrement(ref nextRowIdFieldRef);
			throw Ex_Full_InvOp();
		}
		return nextRowIdFieldRef;

		[MethodImpl(MethodImplOptions.NoInlining)]
		static InvalidOperationException Ex_Full_InvOp() => new("Database full: max rowid reached");
	}

	#endregion

	#endregion
}
