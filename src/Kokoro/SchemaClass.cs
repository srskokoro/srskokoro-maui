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
	private Dictionary<StringKey, FieldInfo>? _FieldInfoChanges;

	[StructLayout(LayoutKind.Auto)]
	public struct FieldInfo {
		private int _Ordinal;
		private FieldStorageType _StorageType;

		public int Ordinal {
			readonly get => _Ordinal;
			set => _Ordinal = value;
		}

		public FieldStorageType StorageType {
			readonly get => _StorageType;
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
			_Uid = value;
			_State = StateFlags.Change_Uid;
		}
	}

	public void SetCachedUid(UniqueId uid) => _Uid = uid;

	public int Ordinal {
		get => _Ordinal;
		set {
			_Ordinal = value;
			_State = StateFlags.Change_Ordinal;
		}
	}

	public void SetCachedOrdinal(int ordinal) => _Ordinal = ordinal;

	public long SrcRowId {
		get => _SrcRowId;
		set {
			_SrcRowId = value;
			_State = StateFlags.Change_SrcRowId;
		}
	}

	public void SetCachedSrcRowId(long srcRowId) => _SrcRowId = srcRowId;

	public string? Name {
		get => _Name;
		set {
			_Name = value;
			_State = StateFlags.Change_Name;
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

		var changes = _FieldInfoChanges;
		if (changes == null) {
			// This becomes a conditional jump forward to not favor it
			goto InitChanged;
		}

	Set:
		infos[name] = info;
		changes[name] = info;
		return;

	Init:
		_FieldInfos = infos = new();
	InitChanged:
		_FieldInfoChanges = changes = new();
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
		using var cmd = db.CreateCommand("""
			SELECT uid,ordinal,src,name FROM SchemaClasses
			WHERE rowid=$rowid
			""");
		cmd.Parameters.AddWithValue("$rowid", _RowId);

		using var r = cmd.ExecuteReader();
		if (r.Read()) {
			// Pending changes will be discarded
			_State = StateFlags.NoChanges;

			r.DAssert_Name(0, "uid");
			_Uid = r.GetUniqueId(0);

			r.DAssert_Name(1, "ordinal");
			_Ordinal = r.GetInt32(1);

			r.DAssert_Name(2, "src");
			_SrcRowId = r.GetInt64(2);

			r.DAssert_Name(3, "name");
			_Name = r.GetString(3);

			return; // Early exit
		}

		// Otherwise, either deleted or never existed
		Unload(); // Let that state materialize here then
		_State = StateFlags.NotExists;
	}

	public void Load(bool core) {
		if (core) Load();
	}

	public void Load(bool core, StringKey fieldName1) {
		var db = Host.Db;
		using (new OptionalReadTransaction(db)) {
			if (core) Load();
			InternalLoadFieldInfo(db, fieldName1);
		}
	}

	public void Load(bool core, StringKey fieldName1, StringKey fieldName2) {
		var db = Host.Db;
		using (new OptionalReadTransaction(db)) {
			if (core) Load();
			InternalLoadFieldInfo(db, fieldName1);
			InternalLoadFieldInfo(db, fieldName2);
		}
	}

	public void Load(bool core, StringKey fieldName1, StringKey fieldName2, StringKey fieldName3) {
		var db = Host.Db;
		using (new OptionalReadTransaction(db)) {
			if (core) Load();
			InternalLoadFieldInfo(db, fieldName1);
			InternalLoadFieldInfo(db, fieldName2);
			InternalLoadFieldInfo(db, fieldName3);
		}
	}

	public void Load(bool core, StringKey fieldName1, StringKey fieldName2, StringKey fieldName3, StringKey fieldName4) {
		var db = Host.Db;
		using (new OptionalReadTransaction(db)) {
			if (core) Load();
			InternalLoadFieldInfo(db, fieldName1);
			InternalLoadFieldInfo(db, fieldName2);
			InternalLoadFieldInfo(db, fieldName3);
			InternalLoadFieldInfo(db, fieldName4);
		}
	}

	public void Load(bool core, StringKey fieldName1, StringKey fieldName2, StringKey fieldName3, StringKey fieldName4, StringKey fieldName5) {
		var db = Host.Db;
		using (new OptionalReadTransaction(db)) {
			if (core) Load();
			InternalLoadFieldInfo(db, fieldName1);
			InternalLoadFieldInfo(db, fieldName2);
			InternalLoadFieldInfo(db, fieldName3);
			InternalLoadFieldInfo(db, fieldName4);
			InternalLoadFieldInfo(db, fieldName5);
		}
	}

	public void Load(bool core, StringKey fieldName1, StringKey fieldName2, StringKey fieldName3, StringKey fieldName4, StringKey fieldName5, StringKey fieldName6) {
		var db = Host.Db;
		using (new OptionalReadTransaction(db)) {
			if (core) Load();
			InternalLoadFieldInfo(db, fieldName1);
			InternalLoadFieldInfo(db, fieldName2);
			InternalLoadFieldInfo(db, fieldName3);
			InternalLoadFieldInfo(db, fieldName4);
			InternalLoadFieldInfo(db, fieldName5);
			InternalLoadFieldInfo(db, fieldName6);
		}
	}

	public void Load(bool core, StringKey fieldName1, StringKey fieldName2, StringKey fieldName3, StringKey fieldName4, StringKey fieldName5, StringKey fieldName6, StringKey fieldName7) {
		var db = Host.Db;
		using (new OptionalReadTransaction(db)) {
			if (core) Load();
			InternalLoadFieldInfo(db, fieldName1);
			InternalLoadFieldInfo(db, fieldName2);
			InternalLoadFieldInfo(db, fieldName3);
			InternalLoadFieldInfo(db, fieldName4);
			InternalLoadFieldInfo(db, fieldName5);
			InternalLoadFieldInfo(db, fieldName6);
			InternalLoadFieldInfo(db, fieldName7);
		}
	}

	public void Load(bool core, StringKey fieldName1, StringKey fieldName2, StringKey fieldName3, StringKey fieldName4, StringKey fieldName5, StringKey fieldName6, StringKey fieldName7, StringKey fieldName8) {
		var db = Host.Db;
		using (new OptionalReadTransaction(db)) {
			if (core) Load();
			InternalLoadFieldInfo(db, fieldName1);
			InternalLoadFieldInfo(db, fieldName2);
			InternalLoadFieldInfo(db, fieldName3);
			InternalLoadFieldInfo(db, fieldName4);
			InternalLoadFieldInfo(db, fieldName5);
			InternalLoadFieldInfo(db, fieldName6);
			InternalLoadFieldInfo(db, fieldName7);
			InternalLoadFieldInfo(db, fieldName8);
			// TODO A counterpart that loads up to 15 field infos
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void Load(bool core, params StringKey[] fieldNames)
		=> Load(core, fieldNames.AsSpan());

	public void Load(bool core, ReadOnlySpan<StringKey> fieldNames) {
		var db = Host.Db;
		using (new OptionalReadTransaction(db)) {
			if (core) Load();

			// TODO Unroll?
			foreach (var fieldName in fieldNames)
				InternalLoadFieldInfo(db, fieldName);
		}
	}

	public void Load(bool core, IEnumerable<StringKey> fieldNames) {
		var db = Host.Db;
		using (new OptionalReadTransaction(db)) {
			if (core) Load();

			// TODO Unroll?
			foreach (var fieldName in fieldNames)
				InternalLoadFieldInfo(db, fieldName);
		}
	}

	[SkipLocalsInit]
	private void InternalLoadFieldInfo(KokoroSqliteDb db, StringKey fieldName) {
		using var cmd = db.CreateCommand();
		var cmdParams = cmd.Parameters;

		long fld;
		{
			cmd.CommandText = "SELECT rowid FROM FieldNames WHERE name=$name";
			cmdParams.AddWithValue("$name", fieldName.Value);

			using var r = cmd.ExecuteReader();
			if (r.Read()) {
				fld = r.GetInt64(0);
			} else {
				return;
			}
			// TODO Cache and load from cache
		}

		cmdParams.Clear();

		// Load field info
		{
			cmd.CommandText = """
				SELECT ordinal,st FROM SchemaClassToFields
				WHERE cls=$cls AND fld=$fld
				""";
			cmdParams.AddWithValue("$cls", _RowId);
			cmdParams.AddWithValue("$fld", fld);

			using var r = cmd.ExecuteReader();
			if (r.Read()) {
				U.SkipInit(out FieldInfo info);

				r.DAssert_Name(0, "ordinal");
				info.Ordinal = r.GetInt32(0);

				r.DAssert_Name(1, "st");
				info.StorageType = (FieldStorageType)r.GetInt32(1);
				info.StorageType.DAssert_Defined();

				// Pending changes will be discarded
				_FieldInfoChanges?.Remove(fieldName);

				SetCachedFieldInfo(fieldName, info);
			}
		}
	}


	public void Unload() {
		UnloadCoreState();
		UnloadFieldInfos();
	}

	public void UnloadCoreState() {
		_State = default;
		_Uid = default;
		_Ordinal = default;
		_SrcRowId = default;
		_Name = default;
	}

	public void UnloadFieldInfos() {
		var infos = _FieldInfos;
		if (infos != null) {
			infos.Clear();
			_FieldInfoChanges = null;
		}
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

	[SkipLocalsInit]
	public void SaveChanges() {
		var state = _State;
		if (state < 0) goto Missing;

		var db = Host.Db; // Throws if host is already disposed
		using (new NestingWriteTransaction(db)) {
			using var cmd = db.CreateCommand();
			SqliteParameterCollection cmdParams = cmd.Parameters;

			// Save core state
			if (state != StateFlags.NoChanges) {
				cmdParams.AddWithValue("$rowid", _RowId);

				StringBuilder cmdSb = new();
				cmdSb.Append("UPDATE SchemaClasses SET\n");

				if ((state & StateFlags.Change_Uid) != 0) {
					cmdSb.Append("uid=$uid,");
					cmdParams.AddWithValue("$uid", _Uid.ToByteArray());
				}
				if ((state & StateFlags.Change_Ordinal) != 0) {
					cmdSb.Append("ordinal=$ordinal,");
					cmdParams.AddWithValue("$ordinal", _Ordinal);
				}
				if ((state & StateFlags.Change_SrcRowId) != 0) {
					cmdSb.Append("src=$src,");
					cmdParams.AddWithValue("$src", RowIds.Box(_SrcRowId));
				}
				if ((state & StateFlags.Change_Name) != 0) {
					cmdSb.Append("name=$name,");
					cmdParams.AddWithValue("$name", _Name);
				}

				Debug.Assert(cmdSb[^1] == ',', $"No changes to save: `{nameof(_State)} == {state}`");
				cmdSb.Remove(cmdSb.Length - 1, 1); // Remove final comma

				cmdSb.Append("\nWHERE rowid=$rowid");
				cmd.CommandText = cmdSb.ToString();

				int updated = cmd.ExecuteNonQuery();
				if (updated != 0) {
					Debug.Assert(updated is 1 or 0);
					_State = StateFlags.NoChanges; // Changes saved successfully
				} else
					goto Missing;
			}

			// Save field infos
			{
				var changes = _FieldInfoChanges;
				if (changes != null) {
					foreach (var (fieldName, info) in changes) {
						long fld;
						{
							cmd.CommandText = "SELECT rowid FROM FieldNames WHERE name=$name";
							cmdParams.Clear();
							cmdParams.AddWithValue("$name", fieldName.Value);

							var r = cmd.ExecuteReader();
							// ^ No `using` since the reader will be disposed
							// automatically anyway if either the command text
							// changes or the command object is disposed.

							if (r.Read()) {
								fld = r.GetInt64(0);
								goto UpdateFieldInfo;
							} else {
								goto InsertNewFieldName;
							}

							// TODO Cache and load from cache
						}

					InsertNewFieldName:
						{
							cmd.CommandText = "INSERT INTO FieldNames(rowid,name) VALUES($rowid,$name)";
							cmdParams.Clear();
							cmdParams.AddWithValue("$rowid", fld = Host.Context.NextFieldNameRowId());
							cmdParams.AddWithValue("$name", fieldName.Value);

							int updated = cmd.ExecuteNonQuery(); // Shouldn't normally fail
							Debug.Assert(updated == 1, $"Updated: {updated}");
						}

					UpdateFieldInfo:
						{
							cmd.CommandText = """
								INSERT INTO SchemaClassToFields(cls,fld,ordinal,st)
								VALUES($cls,$fld,$ordinal,$st)
								ON CONFLICT DO UPDATE
								SET ordinal=$ordinal,st=$st
								""";
							cmdParams.Clear();
							cmdParams.AddWithValue("$cls", _RowId);
							cmdParams.AddWithValue("$fld", fld);
							cmdParams.AddWithValue("$ordinal", info.Ordinal);
							cmdParams.AddWithValue("$st", info.StorageType);
						}
					}
					// Loop end

					changes.Clear(); // Changes saved successfully
				}
			}
		}

		// Success!
		return;

	Missing:
		E_CannotUpdate_MRec(_RowId);

		[DoesNotReturn]
		static void E_CannotUpdate_MRec(long rowid) => throw new MissingRecordException(
			$"Cannot update `{nameof(SchemaClass)}` with rowid {rowid} as it's missing.");
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
