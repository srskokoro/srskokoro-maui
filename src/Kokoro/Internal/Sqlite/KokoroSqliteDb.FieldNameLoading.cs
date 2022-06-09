namespace Kokoro.Internal.Sqlite;
using Kokoro.Internal.Caching;
using Microsoft.Data.Sqlite;

partial class KokoroSqliteDb {
	// TODO-FIXME Should ensure that caches are never stale by preventing field names from being remapped to a different
	// rowid so long as the DB or context exists.
	// - Alt., maintain the cache so long as the SQLite DB's data version still matches.
	private readonly FieldNameToIdCache _FieldNameToIdCache = new(2048);
	private readonly FieldIdToNameCache _FieldIdToNameCache = new(2048);

	internal long LoadFieldId(StringKey fieldName) {
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

	internal StringKey? LoadFieldName(long fieldId) {
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


	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal long InsertFieldNameOrThrow(StringKey fieldName)
		=> InsertFieldNameOrThrow(fieldName.Value);

	[SkipLocalsInit]
	internal long InsertFieldNameOrThrow(string fieldName) {
		using var cmd = CreateCommand();
		// TODO-FIXME Will throw on race
		// TODO Avoid race by wrapping inside an `OptionalReadTransaction` then performing a `SELECT` followed by an `INSERT` if not yet existing
		// - Perhaps also rename it into `EnsureFieldName()`
		cmd.Set("INSERT INTO FieldName(rowid,name) VALUES($rowid,$name)");

		long fieldId = Context!.NextFieldNameRowId();
		var cmdParams = cmd.Params();
		cmdParams.Add(new("$rowid", fieldId));
		cmdParams.Add(new("$name", fieldName));

		try {
			int updated = cmd.ExecuteNonQuery();
			Debug.Assert(updated == 1, $"Updated: {updated}");
			return fieldId;

		} catch (Exception ex) when (
			ex is not SqliteException sqlex ||
			sqlex.SqliteExtendedErrorCode != SQLitePCL.raw.SQLITE_CONSTRAINT_ROWID
		) {
			if (ex is SqliteException sqlex2 && sqlex2.SqliteExtendedErrorCode == SQLitePCL.raw.SQLITE_CONSTRAINT_UNIQUE) {
				ThrowHelper.ThrowInvalidOperationException();
			}
			Debug.Fail("Shouldn't normally fail.", $"Unexpected: {ex}");
			Context?.UndoFieldNameRowId(fieldId);
			throw;
		}
	}
}
