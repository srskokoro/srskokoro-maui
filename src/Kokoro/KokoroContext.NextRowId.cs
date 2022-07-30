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
		using var cmd = db.CreateCommand(
			"SELECT ifnull(max(rowid), 0) FROM Item UNION ALL\n" +
			"SELECT ifnull(max(rowid), 0) FROM Schema UNION ALL\n" +
			"SELECT ifnull(max(rowid), 0) FROM Class UNION ALL\n" +
			"SELECT ifnull(max(rowid), 0) FROM FieldName"
		);

		using var reader = cmd.ExecuteReader();
		reader.Read(); _NextItemId = reader.GetInt64(0);
		reader.Read(); _NextSchemaId = reader.GetInt64(0);
		reader.Read(); _NextClassId = reader.GetInt64(0);
		reader.Read(); _NextFieldId = reader.GetInt64(0);
	}

	private long _NextItemId;
	private long _NextSchemaId;
	private long _NextClassId;
	private long _NextFieldId;

	internal long NextItemId() => NextRowId(ref _NextItemId);
	internal long NextSchemaId() => NextRowId(ref _NextSchemaId);
	internal long NextClassId() => NextRowId(ref _NextClassId);
	internal long NextFieldId() => NextRowId(ref _NextFieldId);

	internal void UndoItemId(long rowidToUndo) => UndoRowId(ref _NextItemId, rowidToUndo);
	internal void UndoSchemaId(long rowidToUndo) => UndoRowId(ref _NextSchemaId, rowidToUndo);
	internal void UndoClassId(long rowidToUndo) => UndoRowId(ref _NextClassId, rowidToUndo);
	internal void UndoFieldId(long rowidToUndo) => UndoRowId(ref _NextFieldId, rowidToUndo);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	private static long NextRowId(ref long nextRowIdFieldRef) {
		// The following trick won't work if the user somehow ran this with 2^63
		// threads, either in parallel or with threads suspended at unfortunate
		// moments. That should be deemed impossible due to resource limits, but
		// who knows? Perhaps some high-tech futuristic civilization knows… :P
		long nextRowId = Interlocked.Increment(ref nextRowIdFieldRef);
		if (nextRowId <= 0) {
			Interlocked.Decrement(ref nextRowIdFieldRef);
			E_Full_InvOp();
		}
		return nextRowId;

		[DoesNotReturn]
		static void E_Full_InvOp() => throw new InvalidOperationException("Database full: max rowid reached");
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static void UndoRowId(ref long nextRowIdFieldRef, long rowidToUndo) {
		if (rowidToUndo > 0) {
			Interlocked.CompareExchange(ref nextRowIdFieldRef, rowidToUndo - 1, rowidToUndo);
		}
	}

	#endregion

	#endregion
}
