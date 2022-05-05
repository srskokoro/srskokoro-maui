namespace Kokoro;
using Kokoro.Internal;
using Kokoro.Internal.Sqlite;
using Microsoft.Data.Sqlite;

public sealed class SchemaClass : DataEntity {

	private long _RowId;

	private UniqueId _Uid;
	private int _Ordinal;
	private long _SrcRowId;
	private string? _Name;

	private StateFlags _State;

	[Flags]
	private enum StateFlags {
		None = 0,

		Change_Uid      = 1 << 0,
		Change_Ordinal  = 1 << 1,
		Change_SrcRowId = 1 << 2,
		Change_Name     = 1 << 3,
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public SchemaClass(KokoroCollection host) : base(host) { }

	public SchemaClass(KokoroCollection host, long rowid)
		: this(host) => _RowId = rowid;


	public void SetCachedUid(UniqueId uid) => _Uid = uid;
	public void SetCachedOrdinal(int ordinal) => _Ordinal = ordinal;
	public void SetCachedSrcRowId(long srcRowId) => _SrcRowId = srcRowId;
	public void SetCachedName(string? name) => _Name = name;


	public long RowId {
		get => _RowId;
		set => _RowId = value;
	}

	public UniqueId Uid {
		get => _Uid;
		set {
			_State = StateFlags.Change_Uid;
			_Uid = value;
		}
	}

	public int Ordinal {
		get => _Ordinal;
		set {
			_State = StateFlags.Change_Ordinal;
			_Ordinal = value;
		}
	}

	public long SrcRowId {
		get => _SrcRowId;
		set {
			_State = StateFlags.Change_SrcRowId;
			_SrcRowId = value;
		}
	}

	public string? Name {
		get => _Name;
		set {
			_State = StateFlags.Change_Name;
			_Name = value;
		}
	}


	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static long LoadRowId(KokoroCollection host, UniqueId uid)
		=> LoadRowId(host.Db, uid);

	internal static long LoadRowId(KokoroSqliteDb db, UniqueId uid)
		=> db.ExecScalar<long>(
			"SELECT rowid FROM SchemaClasses WHERE uid=$uid"
			, new SqliteParameter("$uid", uid.ToByteArray()));


	public sealed override void Load() {
		var db = DataToken.OwnerDb;
		_State = default; // Pending changes will be discarded

		using var cmd = db.CreateCommand(
			"""
			SELECT uid,ordinal,src,name
			WHERE rowid=$rowid
			"""
			, new SqliteParameter("$rowid", _RowId));

		using (new OptionalReadTransaction(db)) {
			using var r = cmd.ExecuteReader();
			if (!r.Read()) goto NotFound;

			r.DAssert_Name(1, "uid");
			_Uid = r.GetUniqueId(1);

			r.DAssert_Name(2, "ordinal");
			_Ordinal = r.GetInt32(2);

			r.DAssert_Name(3, "src");
			_SrcRowId = r.GetInt64(3);

			r.DAssert_Name(4, "name");
			_Name = r.GetString(4);

			if (db.UpdateDataToken()) {
				/// NOTE: <see cref="DataEntity.DataToken"/> may already be
				/// disposed at this point.
				goto Invalidated;
			}
		}

		if (IsQuiteFresh) {
			return; // Early exit
		} else {
			goto Invalidated;
		}

	NotFound:
		// Otherwise, record not found: either deleted or never existed.
		UnloadOnlyCore(); // Let that state materialize here then.

	Invalidated:
		UnloadOnlyNonCore();
		RefreshDataMark();
	}

	public sealed override void Unload() {
		UnloadOnlyCore();
		UnloadOnlyNonCore();
	}

	private void UnloadOnlyCore() {
		_State = default;
		_Uid = default;
		_Ordinal = default;
		_SrcRowId = default;
		_Name = default;
	}

	private void UnloadOnlyNonCore() {
		// Nothing (for now)
	}

	public sealed override void Reload() {
		Load();
		UnloadOnlyNonCore();
	}


	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void SaveAsNew() => SaveAsNew(UniqueId.Create());

	public void SaveAsNew(UniqueId uid) {
		var latest = DataToken.Latest; // Throws if collection already disposed
		SaveAsNew(latest.Db!, // Not null if didn't throw above
			latest.Context!.NextSchemaClassRowId(), uid);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void SaveAsNew(long rowid, UniqueId uid)
		=> SaveAsNew(DataToken.OwnerDb, rowid, uid);

	private void SaveAsNew(KokoroSqliteDb db, long rowid, UniqueId uid) {
		try {
			db.Exec("INSERT INTO SchemaClasses" +
				"(rowid,uid,ordinal,src,name)" +
				" VALUES" +
				"($rowid,$uid,$ordinal,$src,$name)"
				, new SqliteParameter("$rowid", rowid)
				, new SqliteParameter("$uid", uid.ToByteArray())
				, new SqliteParameter("$ordinal", _Ordinal)
				, new SqliteParameter("$src", RowIds.Box(_SrcRowId))
				, new SqliteParameter("$name", _Name)
			);
		} catch (Exception ex) when (
			ex is not SqliteException sqlex ||
			sqlex.SqliteExtendedErrorCode != SQLitePCL.raw.SQLITE_CONSTRAINT_ROWID
		) {
			var context = DataToken.OwnerContextOrNull;
			context?.UndoSchemaClassRowId(rowid);
			throw;
		}

		_State = default; // Pending changes are now saved

		_RowId = rowid;
		_Uid = uid;
	}


	public void SaveChanges() {
		throw new NotImplementedException();
	}


	public static bool RenewRowId(KokoroCollection host, long oldRowId) {
		var context = host.Context;
		long newRowId = context.NextSchemaClassRowId();
		return AlterRowId(host.Db, oldRowId, newRowId);
	}

	/// <summary>
	/// Alias for <see cref="RenewRowId(KokoroCollection, long)"/>
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool AlterRowId(KokoroCollection host, long oldRowId)
			=> RenewRowId(host, oldRowId);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool AlterRowId(KokoroCollection host, long oldRowId, long newRowId)
		=> AlterRowId(host.Db, oldRowId, newRowId);

	internal static bool AlterRowId(KokoroSqliteDb db, long oldRowId, long newRowId) {
		int updated;
		try {
			updated = db.Exec(
				"UPDATE SchemaClasses SET rowid=$newRowId WHERE rowid=$oldRowId"
				, new SqliteParameter("$oldRowId", oldRowId)
				, new SqliteParameter("$newRowId", newRowId)
			);
		} catch (Exception ex) when (
			ex is not SqliteException sqlex ||
			sqlex.SqliteExtendedErrorCode != SQLitePCL.raw.SQLITE_CONSTRAINT_ROWID
		) {
			var context = db.DataToken?.OwnerContextOrNull;
			context?.UndoSchemaClassRowId(newRowId);
			throw;
		}
		return updated > 0;
	}


	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool Delete() => DeleteFrom(DataToken.OwnerDb, _RowId);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool DeleteFrom(KokoroCollection host, long rowid)
		=> DeleteFrom(host.Db, rowid);

	internal static bool DeleteFrom(KokoroSqliteDb db, long rowid) => db.Exec(
		"DELETE FROM SchemaClasses WHERE rowid=$rowid"
		, new SqliteParameter("$rowid", rowid)) > 0;

	public static bool DeleteFrom(KokoroCollection host, UniqueId uid) => host.Db.Exec(
		"DELETE FROM SchemaClasses WHERE uid=$uid"
		, new SqliteParameter("$uid", uid)) > 0;
}
