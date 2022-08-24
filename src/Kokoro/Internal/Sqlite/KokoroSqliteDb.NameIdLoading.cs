namespace Kokoro.Internal.Sqlite;
using Kokoro.Internal.Caching;
using Microsoft.Data.Sqlite;

partial class KokoroSqliteDb {
	private readonly NameToNameIdCache _NameToNameIdCache = new(2048);
	private readonly NameIdToNameCache _NameIdToNameCache = new(2048);

	public void ClearNameIdCaches() {
		_NameToNameIdCache.Clear();
		_NameIdToNameCache.Clear();
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool ReloadNameIdCaches() => ReloadCaches();


	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public long LoadNameId(StringKey name) {
		if (!ReloadNameIdCaches() && _NameToNameIdCache.TryGet(name, out long id)) {
			return id;
		}
		return QueryNameId(name);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public StringKey? LoadName(long nameId) {
		if (!ReloadNameIdCaches() && _NameIdToNameCache.TryGet(nameId, out var name)) {
			return name;
		}
		return QueryName(nameId);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public long LoadStaleNameId(StringKey name) {
		if (_NameToNameIdCache.TryGet(name, out long id)) {
			return id;
		}
		return QueryNameId(name);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public StringKey? LoadStaleName(long nameId) {
		if (_NameIdToNameCache.TryGet(nameId, out var name)) {
			return name;
		}
		return QueryName(nameId);
	}

	[SkipLocalsInit]
	private long QueryNameId(StringKey name) {
		long id;
		using (var cmd = CreateCommand()) {
			cmd.Set($"SELECT rowid FROM {Prot.NameId} WHERE name=$name");
			cmd.AddParams(new("$name", name.Value));

			using var r = cmd.ExecuteReader();
			if (r.Read()) {
				id = r.GetInt64(0);
				Debug.Assert(id != 0, "Unexpected zero rowid.");
			} else {
				id = 0;
			}
		}
		_NameToNameIdCache.Put(name, id);
		return id;
	}

	[SkipLocalsInit]
	private StringKey? QueryName(long nameId) {
		StringKey? name;
		using (var cmd = CreateCommand()) {
			cmd.Set($"SELECT name FROM {Prot.NameId} WHERE rowid=$rowid");
			cmd.AddParams(new("$rowid", nameId));

			using var r = cmd.ExecuteReader();
			if (r.Read()) {
				Debug.Assert(nameId != 0, "Unexpected zero rowid.");
				name = new(r.GetString(0));
				name = _NameToNameIdCache.Normalize(name);
				_NameIdToNameCache.Put(nameId, name);
			} else {
				name = null;
			}
		}
		return name;
	}


	/// <remarks>Never returns zero.</remarks>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public long EnsureNameId(StringKey name) {
		if (!ReloadNameIdCaches() && _NameToNameIdCache.TryGet(name, out long id) && id != 0) {
			return id;
		}
		// NOTE: The following never returns zero.
		return QueryOrInsertNameId(name);
	}

	/// <remarks>Never returns zero.</remarks>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public long LoadStaleOrEnsureNameId(StringKey name) {
		if (_NameToNameIdCache.TryGet(name, out long id) && id != 0) {
			return id;
		}
		// NOTE: The following never returns zero.
		return QueryOrInsertNameId(name);
	}

	/// <remarks>Never returns zero.</remarks>
	[SkipLocalsInit]
	private long QueryOrInsertNameId(StringKey name) {
		using var tx = new NestingWriteTransaction(this);

		long id;
		using (var cmd = CreateCommand()) {
			cmd.Set($"SELECT rowid FROM {Prot.NameId} WHERE name=$name");
			cmd.AddParams(new("$name", name.Value));

			using var r = cmd.ExecuteReader();
			if (r.Read()) {
				id = r.GetInt64(0);
			} else {
				goto InsertNewNameId;
			}
		}

		Debug.Assert(id != 0, "Unexpected zero rowid.");
		_NameToNameIdCache.Put(name, id);
		tx.DisposeNoInvalidate();
		return id;

	InsertNewNameId:
		using (var cmd = CreateCommand()) {
			cmd.Set(
				$"INSERT INTO {Prot.NameId}(rowid,name) VALUES($rowid,$name)"
			).AddParams(
				new("$rowid", id = Context!.NextNameId()),
				new("$name", name.Value)
			);

			try {
				int updated = cmd.ExecuteNonQuery();
				Debug.Assert(updated == 1, $"Updated: {updated}");
			} catch (Exception ex) when (
				ex is not SqliteException sqlex ||
				sqlex.SqliteExtendedErrorCode != SQLitePCL.raw.SQLITE_CONSTRAINT_ROWID
			) {
				// Shouldn't fail via uniqueness constraint, since we already
				// know that the name id entry didn't exist beforehand, and
				// we're in a transaction.
				Debug.Assert(
					ex is not SqliteException sqlex2 ||
					sqlex2.SqliteExtendedErrorCode != SQLitePCL.raw.SQLITE_CONSTRAINT_UNIQUE,
					"Shouldn't normally fail.", $"Unexpected: {ex}"
				);
				Context?.UndoNameId(id);
				throw;
			}
		}

		Debug.Assert(id != 0, "Unexpected zero rowid.");
		_NameToNameIdCache.Put(name, id);
		tx.Commit();
		return id;
	}
}
