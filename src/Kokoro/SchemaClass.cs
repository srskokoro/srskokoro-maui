﻿namespace Kokoro;
using Kokoro.Internal;
using Kokoro.Internal.Sqlite;
using Microsoft.Data.Sqlite;
using System.Runtime.InteropServices;

public sealed class SchemaClass : DataEntity {

	private long _RowId;

	private UniqueId _Uid;
	private int _Ordinal;
	private long _SrcRowId;
	private string? _Name;

	private StateFlags _State;

	[Flags]
	private enum StateFlags : int {
		NoChanges = 0,

		Change_Uid      = 1 << 0,
		Change_Ordinal  = 1 << 1,
		Change_SrcRowId = 1 << 2,
		Change_Name     = 1 << 3,

		NotExists       = 1 << 31,
	}

	private Dictionary<StringKey, FieldInfo>? _FieldInfos;
	private HashSet<StringKey>? _FieldInfosChanged;

	[StructLayout(LayoutKind.Auto)]
	public struct FieldInfo {
		private int _Ordinal;
		private FieldStorageType _StorageType;

		public int Ordinal {
			get => _Ordinal;
			set => _Ordinal = value;
		}

		public FieldStorageType StorageType {
			get => _StorageType;
			set => _StorageType = value;
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public SchemaClass(KokoroCollection host) : base(host) { }

	public SchemaClass(KokoroCollection host, long rowid)
		: this(host) => _RowId = rowid;


	public bool Exists {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		// Ternary operator returning true/false prevents redundant asm generation:
		// See, https://github.com/dotnet/runtime/issues/4207#issuecomment-147184273
		get => _State < 0 ? false : true;
	}

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

	public void SetCachedUid(UniqueId uid) => _Uid = uid;

	public int Ordinal {
		get => _Ordinal;
		set {
			_State = StateFlags.Change_Ordinal;
			_Ordinal = value;
		}
	}

	public void SetCachedOrdinal(int ordinal) => _Ordinal = ordinal;

	public long SrcRowId {
		get => _SrcRowId;
		set {
			_State = StateFlags.Change_SrcRowId;
			_SrcRowId = value;
		}
	}

	public void SetCachedSrcRowId(long srcRowId) => _SrcRowId = srcRowId;

	public string? Name {
		get => _Name;
		set {
			_State = StateFlags.Change_Name;
			_Name = value;
		}
	}

	public void SetCachedName(string? name) => _Name = name;


	public bool TryGetFieldInfo(StringKey name, [MaybeNullWhen(false)] out FieldInfo info) {
		var infos = _FieldInfos;
		if (infos != null && infos.TryGetValue(name, out info)) {
			return true;
		}
		U.SkipInit(out info);
		return false;
	}

	public void SetFieldInfo(StringKey name, FieldInfo info) {
		var infos = _FieldInfos;
		if (infos == null) {
			// This becomes a conditional jump forward to not favor it
			goto Init;
		}

		var changed = _FieldInfosChanged;
		if (changed == null) {
			// This becomes a conditional jump forward to not favor it
			goto InitChanged;
		}

	Set:
		infos[name] = info;
		changed.Add(name);
		return;

	Init:
		_FieldInfos = infos = new();
	InitChanged:
		_FieldInfosChanged = changed = new();
		goto Set;
	}

	public void SetCachedFieldInfo(StringKey name, FieldInfo info) {
		var infos = _FieldInfos;
		if (infos == null) {
			// This becomes a conditional jump forward to not favor it
			goto Init;
		}

	Set:
		infos[name] = info;
		return;

	Init:
		_FieldInfos = infos = new();
		goto Set;
	}


	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static long LoadRowId(KokoroCollection host, UniqueId uid)
		=> LoadRowId(host.Db, uid);

	internal static long LoadRowId(KokoroSqliteDb db, UniqueId uid)
		=> db.ExecScalar<long>(
			"SELECT rowid FROM SchemaClasses WHERE uid=$uid"
			, new SqliteParameter("$uid", uid.ToByteArray()));


	public void Load() {
		var db = Host.Db;
		using var cmd = db.CreateCommand(
			"""
			SELECT uid,ordinal,src,name
			WHERE rowid=$rowid
			"""
			, new SqliteParameter("$rowid", _RowId));

		{
			using var r = cmd.ExecuteReader();
			if (!r.Read()) goto NotFound;

			// Pending changes will be discarded
			_State = StateFlags.NoChanges;

			r.DAssert_Name(1, "uid");
			_Uid = r.GetUniqueId(1);

			r.DAssert_Name(2, "ordinal");
			_Ordinal = r.GetInt32(2);

			r.DAssert_Name(3, "src");
			_SrcRowId = r.GetInt64(3);

			r.DAssert_Name(4, "name");
			_Name = r.GetString(4);

			return; // Early exit
		}

	NotFound: // -- either deleted or never existed
		Unload(); // Let that state materialize here then
		_State = StateFlags.NotExists;
	}

	public void Unload() {
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


	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void SaveAsNew() => SaveAsNew(UniqueId.Create());

	public void SaveAsNew(UniqueId uid) {
		var host = Host;
		var db = host.Db; // Throws if host is already disposed
		var context = host.ContextOrNull; // Not null if didn't throw above
		Debug.Assert(context != null);
		SaveAsNew(db, context.NextSchemaClassRowId(), uid);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void SaveAsNew(long rowid, UniqueId uid)
		=> SaveAsNew(Host.Db, rowid, uid);

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
			db.Context?.UndoSchemaClassRowId(rowid);
			throw;
		}

		_State = StateFlags.NoChanges; // Pending changes are now saved

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
			db.Context?.UndoSchemaClassRowId(newRowId);
			throw;
		}

		Debug.Assert(updated is 1 or 0);
		return ((byte)updated).ToUnsafeBool();
	}


	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool Delete() => DeleteFrom(Host.Db, _RowId);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool DeleteFrom(KokoroCollection host, long rowid)
		=> DeleteFrom(host.Db, rowid);

	internal static bool DeleteFrom(KokoroSqliteDb db, long rowid) {
		int deleted = db.Exec(
			"DELETE FROM SchemaClasses WHERE rowid=$rowid"
			, new SqliteParameter("$rowid", rowid));

		Debug.Assert(deleted is 1 or 0);
		return ((byte)deleted).ToUnsafeBool();
	}

	public static bool DeleteFrom(KokoroCollection host, UniqueId uid) {
		int deleted = host.Db.Exec(
			"DELETE FROM SchemaClasses WHERE uid=$uid"
			, new SqliteParameter("$uid", uid));

		Debug.Assert(deleted is 1 or 0);
		return ((byte)deleted).ToUnsafeBool();
	}
}
