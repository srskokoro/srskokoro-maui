namespace Kokoro.Internal.Sqlite;
using Kokoro.Internal.Caching;
using Microsoft.Data.Sqlite;

partial class KokoroSqliteDb {
	private readonly FieldNameToIdCache _FieldNameToIdCache = new(2048);
	private readonly FieldIdToNameCache _FieldIdToNameCache = new(2048);

	public void ClearFieldNameCaches() {
		_FieldNameToIdCache.Clear();
		_FieldIdToNameCache.Clear();
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool ReloadFieldNameCaches() => ReloadCaches();

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public long LoadFieldId(StringKey fieldName) {
		ReloadFieldNameCaches();
		return LoadStaleFieldId(fieldName);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public StringKey? LoadFieldName(long fieldId) {
		ReloadFieldNameCaches();
		return LoadStaleFieldName(fieldId);
	}


	public long LoadStaleFieldId(StringKey fieldName) {
		if (_FieldNameToIdCache.TryGet(fieldName, out long id)) {
			return id;
		}

		using var cmd = QueryForFieldId(fieldName);
		using var r = cmd.ExecuteReader();
		if (r.Read()) {
			id = r.GetInt64(0);
			_FieldNameToIdCache.Put(fieldName, id);
		}
		return 0;
	}

	public StringKey? LoadStaleFieldName(long fieldId) {
		if (_FieldIdToNameCache.TryGet(fieldId, out var name)) {
			return name;
		}

		using var cmd = QueryForFieldName(fieldId);
		using var r = cmd.ExecuteReader();
		if (r.Read()) {
			name = new(r.GetString(0));
			name = _FieldNameToIdCache.Normalize(name);
			_FieldIdToNameCache.Put(fieldId, name);
			return name;
		}
		return null;
	}

	private SqliteCommand QueryForFieldId(StringKey fieldName) {
		var cmd = CreateCommand();
		try {
			cmd.Set("SELECT rowid FROM FieldName WHERE name=$name");
			cmd.Params().Add(new("$name", fieldName.Value));
			return cmd;
		} catch {
			cmd.Dispose();
			throw;
		}
	}

	private SqliteCommand QueryForFieldName(long fieldId) {
		var cmd = CreateCommand();
		try {
			cmd.Set("SELECT name FROM FieldName WHERE rowid=$rowid");
			cmd.Params().Add(new("$rowid", fieldId));
			return cmd;
		} catch {
			cmd.Dispose();
			throw;
		}
	}


	[SkipLocalsInit]
	public long EnsureFieldId(StringKey fieldName) {
		if (!ReloadFieldNameCaches() && _FieldNameToIdCache.TryGet(fieldName, out long id)) {
			return id;
		}

		using var tx = new NestingWriteTransaction(this);

		using (var cmd = QueryForFieldId(fieldName)) {
			using var r = cmd.ExecuteReader();
			if (r.Read()) {
				id = r.GetInt64(0);
			} else {
				goto InsertNewFieldName;
			}
		}

		_FieldNameToIdCache.Put(fieldName, id);
		tx.DisposeNoInvalidate();
		return id;

	InsertNewFieldName:
		using (var cmd = CreateCommand()) {
			cmd.Set(
				"INSERT INTO FieldName(rowid,name) VALUES($rowid,$name)"
			).AddParams(
				new("$rowid", id = Context!.NextFieldNameRowId()),
				new("$name", fieldName.Value));

			try {
				int updated = cmd.ExecuteNonQuery();
				Debug.Assert(updated == 1, $"Updated: {updated}");
			} catch (Exception ex) when (
				ex is not SqliteException sqlex ||
				sqlex.SqliteExtendedErrorCode != SQLitePCL.raw.SQLITE_CONSTRAINT_ROWID
			) {
				// Shouldn't fail via uniqueness constraint, since we already
				// know that the field name didn't exist beforehand, and we're
				// in a transaction.
				Debug.Assert(
					ex is not SqliteException sqlex2 ||
					sqlex2.SqliteExtendedErrorCode != SQLitePCL.raw.SQLITE_CONSTRAINT_UNIQUE,
					"Shouldn't normally fail.", $"Unexpected: {ex}"
				);
				Context?.UndoFieldNameRowId(id);
				throw;
			}
		}

		_FieldNameToIdCache.Put(fieldName, id);
		tx.Commit();
		return id;
	}
}
