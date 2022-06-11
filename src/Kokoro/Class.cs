namespace Kokoro;
using Kokoro.Internal;
using Kokoro.Internal.Sqlite;
using Microsoft.Data.Sqlite;
using System.Runtime.InteropServices;

/// <summary>
/// An entity class, i.e., a classable entity's class.
/// </summary>
public sealed class Class : DataEntity {

	private long _RowId;

	private UniqueId _Uid;
	private int _Ordinal;
	private long _GrpRowId;
	private string? _Name;

	private Dictionary<StringKey, FieldInfo>? _FieldInfos;
	private Dictionary<StringKey, FieldInfo>? _FieldInfoChanges;

	private StateFlags _State;

	public bool Exists {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		// Ternary operator returning true/false prevents redundant asm generation:
		// See, https://github.com/dotnet/runtime/issues/4207#issuecomment-147184273
		get => _State < 0 ? false : true;
	}


	[Flags]
	private enum StateFlags : int {
		NoChanges = 0,

		Change_Uid      = 1 << 0,
		Change_Ordinal  = 1 << 1,
		Change_GrpRowId = 1 << 2,
		Change_Name     = 1 << 3,

		NotExists       = 1 << 31,
	}

	public struct FieldInfo {
		internal bool _IsDeleted;

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


	public Class(KokoroCollection host) : base(host) { }

	public Class(KokoroCollection host, long rowid)
		: this(host) => _RowId = rowid;


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

	public long GrpRowId {
		get => _GrpRowId;
		set {
			_GrpRowId = value;
			_State = StateFlags.Change_GrpRowId;
		}
	}

	public void SetCachedGrpRowId(long grpRowId) => _GrpRowId = grpRowId;

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
			goto InitChanges;
		}

	Set:
		infos[name] = info;
		changes[name] = info;
		return;

	Init:
		_FieldInfos = infos = new();
	InitChanges:
		_FieldInfoChanges = changes = new();
		goto Set;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void DeleteFieldInfo(StringKey name)
		=> SetFieldInfo(name, new() { _IsDeleted = true });

	public void SetCachedFieldInfo(StringKey name, FieldInfo info) {
		var infos = _FieldInfos;
		if (infos == null) {
			// This becomes a conditional jump forward to not favor it
			goto Init;
		}

	Set:
		infos[name] = info;

		{
			var changes = _FieldInfoChanges;
			if (changes != null) {
				ref var infoRef = ref CollectionsMarshal.GetValueRefOrNullRef(changes, name);
				if (!U.IsNullRef(ref infoRef)) {
					infoRef = info;
				}
			}
		}

		return;

	Init:
		_FieldInfos = infos = new();
		goto Set;
	}

	public void UnloadFieldInfo(StringKey name) {
		var infos = _FieldInfos;
		if (infos != null) {
			infos.Remove(name);
			_FieldInfoChanges?.Remove(name);
		}
	}


	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static long LoadRowId(KokoroCollection host, UniqueId uid)
		=> LoadRowId(host.Db, uid);

	internal static long LoadRowId(KokoroSqliteDb db, UniqueId uid)
		=> db.Cmd("SELECT rowid FROM Class WHERE uid=$uid")
			.AddParams(new("$uid", uid.ToByteArray()))
			.ConsumeScalar<long>();


	public void Load() {
		var db = Host.Db;
		using var cmd = db.Cmd("""
			SELECT uid,ord,ifnull(grp,0)AS grp,name FROM Class
			WHERE rowid=$rowid
			""");
		cmd.Parameters.Add(new("$rowid", _RowId));

		using var r = cmd.ExecuteReader();
		if (r.Read()) {
			// Pending changes will be discarded
			_State = StateFlags.NoChanges;

			r.DAssert_Name(0, "uid");
			_Uid = r.GetUniqueId(0);

			r.DAssert_Name(1, "ord");
			_Ordinal = r.GetInt32(1);

			r.DAssert_Name(2, "grp");
			_GrpRowId = r.GetInt64(2);

			r.DAssert_Name(3, "name");
			_Name = r.GetString(3);

			return; // Early exit
		}

		// Otherwise, either deleted or never existed.
		Unload(); // Let that state materialize here then.
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
	private void InternalLoadFieldInfo(KokoroSqliteDb db, StringKey name) {
		using var cmd = db.CreateCommand();
		var cmdParams = cmd.Parameters;

		long fld;
		{
			cmd.CommandText = "SELECT rowid FROM FieldName WHERE name=$name";
			cmdParams.Add(new("$name", name.Value));

			using var r = cmd.ExecuteReader();
			if (r.Read()) {
				fld = r.GetInt64(0);
			} else {
				goto NotFound;
			}
			// TODO Cache and load from cache
		}

		cmdParams.Clear();

		// Load field info
		{
			cmd.Reset("""
				SELECT ord,sto FROM ClassToField
				WHERE cls=$cls AND fld=$fld
				""");
			cmdParams.Add(new("$cls", _RowId));
			cmdParams.Add(new("$fld", fld));

			using var r = cmd.ExecuteReader();
			if (r.Read()) {
				U.SkipInit(out FieldInfo info);

				r.DAssert_Name(0, "ord");
				info.Ordinal = r.GetInt32(0);

				r.DAssert_Name(1, "sto");
				info.StorageType = (FieldStorageType)r.GetInt32(1);
				info.StorageType.DAssert_Defined();

				// Pending changes will be discarded
				_FieldInfoChanges?.Remove(name);

				SetCachedFieldInfo(name, info);

				return; // Early exit
			}
		}

	NotFound:
		// Otherwise, either deleted or never existed.
		// Let that state materialize here then.
		UnloadFieldInfo(name);
	}


	public void Unload() {
		UnloadCoreState();
		UnloadFieldInfos();
	}

	public void UnloadCoreState() {
		_State = default;
		_Uid = default;
		_Ordinal = default;
		_GrpRowId = default;
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

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void SaveAsNew(UniqueId uid) => SaveAsNew(0, uid);

	[SkipLocalsInit]
	public void SaveAsNew(long rowid, UniqueId uid) {
		var db = Host.Db; // Throws if host is already disposed

		bool hasUsedNextRowId;
		if (rowid == 0) {
			// Guaranteed not null if didn't throw above
			Debug.Assert(db.Context != null);
			rowid = db.Context.NextClassRowId();
			hasUsedNextRowId = true;
		} else {
			hasUsedNextRowId = false;
		}

		try {
			using var tx = new NestingWriteTransaction(db);
			using var cmd = db.Cmd(
				"INSERT INTO Class" +
				"(rowid,uid,ord,grp,name)" +
				" VALUES" +
				"($rowid,$uid,$ord,$grp,$name)");

			var cmdParams = cmd.Parameters;
			cmdParams.Add(new("$rowid", rowid));
			cmdParams.Add(new("$uid", uid.ToByteArray()));
			cmdParams.Add(new("$ord", _Ordinal));
			cmdParams.Add(new("$grp", RowIds.Box(_GrpRowId)));
			cmdParams.Add(new("$name", _Name));

			int updated = cmd.ExecuteNonQuery();
			Debug.Assert(updated == 1, $"Updated: {updated}");

			// Save field infos
			{
				var changes = _FieldInfoChanges;
				if (changes != null) {
					InternalSaveFieldInfos(cmd, changes, rowid);

					changes.Clear(); // Changes saved successfully
				}
			}

			_State = StateFlags.NoChanges; // Pending changes are now saved

			// COMMIT (or RELEASE) should be guaranteed to not fail at this
			// point if there's at least one operation that started a write.
			// - See, https://www.sqlite.org/rescode.html#busy
			tx.Commit();

		} catch (Exception ex) when (hasUsedNextRowId && (
			ex is not SqliteException sqlex ||
			sqlex.SqliteExtendedErrorCode != SQLitePCL.raw.SQLITE_CONSTRAINT_ROWID
		)) {
			db.Context?.UndoClassRowId(rowid);
			throw;
		}

		_RowId = rowid;
		_Uid = uid;
	}

	[SkipLocalsInit]
	public void SaveChanges() {
		var state = _State;
		if (state < 0) goto Missing;

		var db = Host.Db; // Throws if host is already disposed
		using (var tx = new NestingWriteTransaction(db)) {
			using var cmd = db.CreateCommand();
			var cmdParams = cmd.Parameters;

			// Save core state
			if (state != StateFlags.NoChanges) {
				cmdParams.Add(new("$rowid", _RowId));

				StringBuilder cmdSb = new();
				cmdSb.Append("UPDATE Class SET\n");

				if ((state & StateFlags.Change_Uid) != 0) {
					cmdSb.Append("uid=$uid,");
					cmdParams.Add(new("$uid", _Uid.ToByteArray()));
				}
				if ((state & StateFlags.Change_Ordinal) != 0) {
					cmdSb.Append("ord=$ord,");
					cmdParams.Add(new("$ord", _Ordinal));
				}
				if ((state & StateFlags.Change_GrpRowId) != 0) {
					cmdSb.Append("grp=$grp,");
					cmdParams.Add(new("$grp", RowIds.Box(_GrpRowId)));
				}
				if ((state & StateFlags.Change_Name) != 0) {
					cmdSb.Append("name=$name,");
					cmdParams.Add(new("$name", _Name));
				}

				Debug.Assert(cmdSb[^1] == ',', $"No changes to save: `{nameof(_State)} == {state}`");
				cmdSb.Remove(cmdSb.Length - 1, 1); // Remove final comma

				cmdSb.Append("\nWHERE rowid=$rowid");
				cmd.CommandText = cmdSb.ToString();

				int updated = cmd.ExecuteNonQuery();
				if (updated != 0) {
					Debug.Assert(updated == 1, $"Updated: {updated}");
				} else
					goto Missing;
			}

			// Save field infos
			{
				var changes = _FieldInfoChanges;
				if (changes != null) {
					InternalSaveFieldInfos(cmd, changes, _RowId);

					changes.Clear(); // Changes saved successfully
				}
			}

			_State = StateFlags.NoChanges; // Pending changes are now saved

			// COMMIT (or RELEASE) should be guaranteed to not fail at this
			// point if there's at least one operation that started a write.
			// - See, https://www.sqlite.org/rescode.html#busy
			tx.Commit();
		}

		// Success!
		return;

	Missing:
		E_CannotUpdate_MRec(_RowId);

		[DoesNotReturn]
		static void E_CannotUpdate_MRec(long rowid) => throw new MissingRecordException(
			$"Cannot update `{nameof(Class)}` with rowid {rowid} as it's missing.");
	}

	[SkipLocalsInit]
	private void InternalSaveFieldInfos(SqliteCommand cmd, Dictionary<StringKey, FieldInfo> fieldInfoChanges, long clsRowId) {
		var cmdParams = cmd.Parameters;
		foreach (var (fieldName, info) in fieldInfoChanges) {
			long fld;
			{
				cmd.Reset("SELECT rowid FROM FieldName WHERE name=$name");
				cmdParams.Clear();
				cmdParams.Add(new("$name", fieldName.Value));

				var r = cmd.ExecuteReader();
				// ^ No `using` since the reader will be disposed
				// automatically anyway if either the command text
				// changes or the command object is disposed.

				if (r.Read()) {
					fld = r.GetInt64(0);
					if (!info._IsDeleted) {
						goto UpdateFieldInfo;
					} else {
						goto DeleteFieldInfo;
					}
				} else {
					if (!info._IsDeleted) {
						goto InsertNewFieldName;
					} else {
						// Deletion requested, but there's nothing to delete, as
						// the field is nonexistent.
						continue;
					}
				}

				// TODO Cache field rowids and load from cache
			}

		InsertNewFieldName:
			{
				cmd.Reset("INSERT INTO FieldName(rowid,name) VALUES($rowid,$name)");
				cmdParams.Clear();
				cmdParams.Add(new("$rowid", fld = Host.Context.NextFieldNameRowId()));
				cmdParams.Add(new("$name", fieldName.Value));

				int updated = cmd.ExecuteNonQuery(); // Shouldn't normally fail
				Debug.Assert(updated == 1, $"Updated: {updated}");
			}

		UpdateFieldInfo:
			{
				cmd.Reset("""
					INSERT INTO ClassToField(cls,fld,ord,sto)
					VALUES($cls,$fld,$ord,$sto)
					ON CONFLICT DO UPDATE
					SET ord=$ord,sto=$sto
					""");
				cmdParams.Clear();
				cmdParams.Add(new("$cls", clsRowId));
				cmdParams.Add(new("$fld", fld));
				cmdParams.Add(new("$ord", info.Ordinal));
				cmdParams.Add(new("$sto", info.StorageType));

				int updated = cmd.ExecuteNonQuery();
				Debug.Assert(updated == 1, $"Updated: {updated}");

				continue;
			}

		DeleteFieldInfo:
			{
				cmd.Reset("DELETE FROM ClassToField WHERE (cls,fld)=($cls,$fld)");
				cmdParams.Clear();
				cmdParams.Add(new("$cls", clsRowId));
				cmdParams.Add(new("$fld", fld));

				int deleted = cmd.ExecuteNonQuery();
				// NOTE: It's possible for nothing to be deleted, for when the
				// field doesn't exist in the first place yet we have it cached.
				//
				// TODO Cache field rowids and load from cache
				Debug.Assert(deleted is 1 or 0);
			}
		}
		// Loop end
	}


	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void RenewRowId(KokoroCollection host, long oldRowId)
		=> AlterRowId(host, oldRowId, 0);

	/// <summary>
	/// Alias for <see cref="RenewRowId(KokoroCollection, long)"/>
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void AlterRowId(KokoroCollection host, long oldRowId)
		=> RenewRowId(host, oldRowId);

	[SkipLocalsInit]
	public static void AlterRowId(KokoroCollection host, long oldRowId, long newRowId) {
		var db = host.Db; // Throws if host is already disposed

		bool hasUsedNextRowId;
		if (newRowId == 0) {
			// Guaranteed not null if didn't throw above
			Debug.Assert(db.Context != null);
			newRowId = db.Context.NextClassRowId();
			hasUsedNextRowId = true;
		} else {
			hasUsedNextRowId = false;
		}

		int updated;
		try {
			updated = db.Cmd("UPDATE Class SET rowid=$newRowId WHERE rowid=$oldRowId")
				.AddParams(new("$oldRowId", oldRowId), new("$newRowId", newRowId))
				.Consume();
		} catch (Exception ex) when (hasUsedNextRowId && (
			ex is not SqliteException sqlex ||
			sqlex.SqliteExtendedErrorCode != SQLitePCL.raw.SQLITE_CONSTRAINT_ROWID
		)) {
			db.Context?.UndoClassRowId(newRowId);
			throw;
		}

		if (updated != 0) {
			Debug.Assert(updated == 1, $"Updated: {updated}");
			return;
		}

		// Otherwise, record missing -- either deleted or never existed.
		E_CannotUpdate_MRec(oldRowId);

		[DoesNotReturn]
		static void E_CannotUpdate_MRec(long rowid) => throw new MissingRecordException(
			$"Cannot update `{nameof(Class)}` with rowid {rowid} as it's missing.");
	}


	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool Delete() => DeleteFrom(Host, _RowId);

	public static bool DeleteFrom(KokoroCollection host, long rowid) {
		var db = host.Db;
		int deleted = db.Cmd("DELETE FROM Class WHERE rowid=$rowid")
			.AddParams(new("$rowid", rowid)).Consume();

		Debug.Assert(deleted is 1 or 0);
		return ((byte)deleted).ToUnsafeBool();
	}

	public static bool DeleteFrom(KokoroCollection host, UniqueId uid) {
		var db = host.Db;
		int deleted = db.Cmd("DELETE FROM Class WHERE uid=$uid")
			.AddParams(new("$uid", uid)).Consume();

		Debug.Assert(deleted is 1 or 0);
		return ((byte)deleted).ToUnsafeBool();
	}
}
