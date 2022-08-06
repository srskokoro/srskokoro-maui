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
		if (!ReloadFieldNameCaches() && _FieldNameToIdCache.TryGet(fieldName, out long id)) {
			return id;
		}
		return QueryFieldId(fieldName);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public StringKey? LoadFieldName(long fieldId) {
		if (!ReloadFieldNameCaches() && _FieldIdToNameCache.TryGet(fieldId, out var name)) {
			return name;
		}
		return QueryFieldName(fieldId);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public long LoadStaleFieldId(StringKey fieldName) {
		if (_FieldNameToIdCache.TryGet(fieldName, out long id)) {
			return id;
		}
		return QueryFieldId(fieldName);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public StringKey? LoadStaleFieldName(long fieldId) {
		if (_FieldIdToNameCache.TryGet(fieldId, out var name)) {
			return name;
		}
		return QueryFieldName(fieldId);
	}

	[SkipLocalsInit]
	private long QueryFieldId(StringKey fieldName) {
		using var cmd = CreateCommand();
		cmd.Set("SELECT rowid FROM NameId WHERE name=$name");
		cmd.AddParams(new("$name", fieldName.Value));

		using var r = cmd.ExecuteReader();
		if (r.Read()) {
			long id = r.GetInt64(0);
			Debug.Assert(id != 0, "Unexpected zero rowid.");
			_FieldNameToIdCache.Put(fieldName, id);
		}
		return 0;
	}

	[SkipLocalsInit]
	private StringKey? QueryFieldName(long fieldId) {
		using var cmd = CreateCommand();
		cmd.Set("SELECT name FROM NameId WHERE rowid=$rowid");
		cmd.AddParams(new("$rowid", fieldId));

		using var r = cmd.ExecuteReader();
		if (r.Read()) {
			Debug.Assert(fieldId != 0, "Unexpected zero rowid.");
			StringKey name = new(r.GetString(0));
			name = _FieldNameToIdCache.Normalize(name);
			_FieldIdToNameCache.Put(fieldId, name);
			return name;
		}
		return null;
	}


	/// <remarks>Never returns zero.</remarks>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public long EnsureFieldId(StringKey fieldName) {
		if (!ReloadFieldNameCaches() && _FieldNameToIdCache.TryGet(fieldName, out long id)) {
			Debug.Assert(id != 0, "Unexpected zero rowid in cache.");
			return id;
		}
		// NOTE: The following never returns zero.
		return QueryOrInsertFieldId(fieldName);
	}

	/// <remarks>Never returns zero.</remarks>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public long LoadStaleOrEnsureFieldId(StringKey fieldName) {
		if (_FieldNameToIdCache.TryGet(fieldName, out long id)) {
			Debug.Assert(id != 0, "Unexpected zero rowid in cache.");
			return id;
		}
		// NOTE: The following never returns zero.
		return QueryOrInsertFieldId(fieldName);
	}

	/// <remarks>Never returns zero.</remarks>
	[SkipLocalsInit]
	private long QueryOrInsertFieldId(StringKey fieldName) {
		using var tx = new NestingWriteTransaction(this);

		long id;
		using (var cmd = CreateCommand()) {
			cmd.Set("SELECT rowid FROM NameId WHERE name=$name");
			cmd.AddParams(new("$name", fieldName.Value));

			using var r = cmd.ExecuteReader();
			if (r.Read()) {
				id = r.GetInt64(0);
			} else {
				goto InsertNewFieldName;
			}
		}

		Debug.Assert(id != 0, "Unexpected zero rowid.");
		_FieldNameToIdCache.Put(fieldName, id);
		tx.DisposeNoInvalidate();
		return id;

	InsertNewFieldName:
		using (var cmd = CreateCommand()) {
			cmd.Set(
				"INSERT INTO NameId(rowid,name) VALUES($rowid,$name)"
			).AddParams(
				new("$rowid", id = Context!.NextFieldId()),
				new("$name", fieldName.Value)
			);

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
				Context?.UndoFieldId(id);
				throw;
			}
		}

		Debug.Assert(id != 0, "Unexpected zero rowid.");
		_FieldNameToIdCache.Put(fieldName, id);
		tx.Commit();
		return id;
	}
}
