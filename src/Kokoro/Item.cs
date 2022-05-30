namespace Kokoro;
using Kokoro.Internal;
using Kokoro.Internal.Marshal.Fields;
using Kokoro.Internal.Sqlite;
using Microsoft.Data.Sqlite;
using System.Runtime.InteropServices;

public sealed class Item : DataEntity {

	private long _RowId;
	private UniqueId _Uid;

	private long _ParentRowId;
	private int _Ordinal;

	private ulong _OrdModStamp;

	private long _SchemaRowId;
	private Dictionary<StringKey, FieldVal>? _Fields;
	private Dictionary<StringKey, FieldVal>? _FieldChanges;

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

		Change_Uid         = 1 << 0,
		Change_ParentRowId = 1 << 1,
		Change_Ordinal     = 1 << 2,
		Change_OrdModStamp = 1 << 3,
		Change_SchemaRowId = 1 << 4,

		NotExists          = 1 << 31,
	}


	public Item(KokoroCollection host) : base(host) { }

	public Item(KokoroCollection host, long rowid)
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

	public long ParentRowId {
		get => _ParentRowId;
		set {
			_ParentRowId = value;
			_State = StateFlags.Change_ParentRowId;
		}
	}

	public void SetCachedParentRowId(long parentRowId) => _ParentRowId = parentRowId;

	public int Ordinal {
		get => _Ordinal;
		set {
			_Ordinal = value;
			_State = StateFlags.Change_Ordinal;
		}
	}

	public void SetCachedOrdinal(int ordinal) => _Ordinal = ordinal;

	public ulong OrdModStamp {
		get => _OrdModStamp;
		set {
			_OrdModStamp = value;
			_State = StateFlags.Change_OrdModStamp;
		}
	}

	public void SetCachedOrdModStamp(ulong ordModStamp) => _OrdModStamp = ordModStamp;

	public long SchemaRowId {
		get => _SchemaRowId;
		set {
			_SchemaRowId = value;
			_State = StateFlags.Change_SchemaRowId;
		}
	}

	public void SetCachedSchemaRowId(long schemaRowId) => _SchemaRowId = schemaRowId;


	public bool TryGet(StringKey name, [MaybeNullWhen(false)] out FieldVal value) {
		var fields = _Fields;
		if (fields != null && fields.TryGetValue(name, out value)) {
			return true;
		}
		U.SkipInit(out value);
		return false;
	}

	public void Set(StringKey name, FieldVal value) {
		var fields = _Fields;
		if (fields == null) {
			// This becomes a conditional jump forward to not favor it
			goto Init;
		}

		var changes = _FieldChanges;
		if (changes == null) {
			// This becomes a conditional jump forward to not favor it
			goto InitChanges;
		}

	Set:
		fields[name] = value;
		changes[name] = value;
		return;

	Init:
		_Fields = fields = new();
	InitChanges:
		_FieldChanges = changes = new();
		goto Set;
	}

	public void SetCache(StringKey name, FieldVal value) {
		var fields = _Fields;
		if (fields == null) {
			// This becomes a conditional jump forward to not favor it
			goto Init;
		}

	Set:
		fields[name] = value;

		{
			var changes = _FieldChanges;
			if (changes != null) {
				ref var valueRef = ref CollectionsMarshal.GetValueRefOrNullRef(changes, name);
				if (!U.IsNullRef(ref valueRef)) {
					valueRef = value;
				}
			}
		}

		return;

	Init:
		_Fields = fields = new();
		goto Set;
	}


	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static long LoadRowId(KokoroCollection host, UniqueId uid)
		=> LoadRowId(host.Db, uid);

	internal static long LoadRowId(KokoroSqliteDb db, UniqueId uid)
		=> db.Cmd("SELECT rowid FROM Item WHERE uid=$uid")
			.AddParams(new("$uid", uid.ToByteArray()))
			.ConsumeScalar<long>();


	public void Load() {
		var db = Host.Db;
		using var cmd = db.Cmd("""
			SELECT uid,parent,ord,ord_modst,schema FROM Item
			WHERE rowid=$rowid
			""");
		cmd.Parameters.Add(new("$rowid", _RowId));

		using var r = cmd.ExecuteReader();
		if (r.Read()) {
			// Pending changes will be discarded
			_State = StateFlags.NoChanges;

			r.DAssert_Name(0, "uid");
			_Uid = r.GetUniqueId(0);

			r.DAssert_Name(1, "parent");
			_ParentRowId = r.GetInt64(1);

			r.DAssert_Name(2, "ord");
			_Ordinal = r.GetInt32(2);

			r.DAssert_Name(3, "ord_modst");
			_OrdModStamp = (ulong)r.GetInt64(3);

			r.DAssert_Name(4, "schema");
			_SchemaRowId = r.GetInt64(4);

			return; // Early exit
		}

		// Otherwise, either deleted or never existed.
		Unload(); // Let that state materialize here then.
		_State = StateFlags.NotExists;
	}

	public void Load(StringKey fieldName1) {
		var db = Host.Db;
		using (new OptionalReadTransaction(db)) {
			Load();
			InternalLoadField(db, fieldName1);
		}
	}

	public void Load(StringKey fieldName1, StringKey fieldName2) {
		var db = Host.Db;
		using (new OptionalReadTransaction(db)) {
			Load();
			InternalLoadField(db, fieldName1);
			InternalLoadField(db, fieldName2);
		}
	}

	public void Load(StringKey fieldName1, StringKey fieldName2, StringKey fieldName3) {
		var db = Host.Db;
		using (new OptionalReadTransaction(db)) {
			Load();
			InternalLoadField(db, fieldName1);
			InternalLoadField(db, fieldName2);
			InternalLoadField(db, fieldName3);
		}
	}

	public void Load(StringKey fieldName1, StringKey fieldName2, StringKey fieldName3, StringKey fieldName4) {
		var db = Host.Db;
		using (new OptionalReadTransaction(db)) {
			Load();
			InternalLoadField(db, fieldName1);
			InternalLoadField(db, fieldName2);
			InternalLoadField(db, fieldName3);
			InternalLoadField(db, fieldName4);
		}
	}

	public void Load(StringKey fieldName1, StringKey fieldName2, StringKey fieldName3, StringKey fieldName4, StringKey fieldName5) {
		var db = Host.Db;
		using (new OptionalReadTransaction(db)) {
			Load();
			InternalLoadField(db, fieldName1);
			InternalLoadField(db, fieldName2);
			InternalLoadField(db, fieldName3);
			InternalLoadField(db, fieldName4);
			InternalLoadField(db, fieldName5);
		}
	}

	public void Load(StringKey fieldName1, StringKey fieldName2, StringKey fieldName3, StringKey fieldName4, StringKey fieldName5, StringKey fieldName6) {
		var db = Host.Db;
		using (new OptionalReadTransaction(db)) {
			Load();
			InternalLoadField(db, fieldName1);
			InternalLoadField(db, fieldName2);
			InternalLoadField(db, fieldName3);
			InternalLoadField(db, fieldName4);
			InternalLoadField(db, fieldName5);
			InternalLoadField(db, fieldName6);
		}
	}

	public void Load(StringKey fieldName1, StringKey fieldName2, StringKey fieldName3, StringKey fieldName4, StringKey fieldName5, StringKey fieldName6, StringKey fieldName7) {
		var db = Host.Db;
		using (new OptionalReadTransaction(db)) {
			Load();
			InternalLoadField(db, fieldName1);
			InternalLoadField(db, fieldName2);
			InternalLoadField(db, fieldName3);
			InternalLoadField(db, fieldName4);
			InternalLoadField(db, fieldName5);
			InternalLoadField(db, fieldName6);
			InternalLoadField(db, fieldName7);
		}
	}

	public void Load(StringKey fieldName1, StringKey fieldName2, StringKey fieldName3, StringKey fieldName4, StringKey fieldName5, StringKey fieldName6, StringKey fieldName7, StringKey fieldName8) {
		var db = Host.Db;
		using (new OptionalReadTransaction(db)) {
			Load();
			InternalLoadField(db, fieldName1);
			InternalLoadField(db, fieldName2);
			InternalLoadField(db, fieldName3);
			InternalLoadField(db, fieldName4);
			InternalLoadField(db, fieldName5);
			InternalLoadField(db, fieldName6);
			InternalLoadField(db, fieldName7);
			InternalLoadField(db, fieldName8);
			// TODO A counterpart that loads up to 16 fields
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void Load(params StringKey[] fieldNames)
		=> Load(fieldNames.AsSpan());

	public void Load(ReadOnlySpan<StringKey> fieldNames) {
		var db = Host.Db;
		using (new OptionalReadTransaction(db)) {
			Load();

			// TODO Unroll?
			foreach (var fieldName in fieldNames)
				InternalLoadField(db, fieldName);
		}
	}

	[SkipLocalsInit]
	private void InternalLoadField(KokoroSqliteDb db, StringKey name) {
		using var cmd = db.CreateCommand();
		var cmdParams = cmd.Parameters;

		long fld;
		{
			cmd.Set("SELECT rowid FROM FieldName WHERE name=$name");
			cmdParams.Add(new("$name", name.Value));

			using var r = cmd.ExecuteReader();
			if (r.Read()) {
				fld = r.GetInt64(0);
			} else {
				goto NotFound;
			}
			// TODO Cache and load from cache
		}

		int idx;
		FieldStorageType sto;
		{
			cmd.Reset("""
				SELECT idx_sto FROM SchemaToField
				WHERE schema=$schema AND fld=$fld
				""");
			cmdParams.Clear();

			// NOTE: Requires a preloaded core state
			cmdParams.Add(new("$schema", _SchemaRowId));
			cmdParams.Add(new("$fld", fld));

			using var r = cmd.ExecuteReader();
			if (r.Read()) {
				r.DAssert_Name(0, "idx_sto");
				uint idx_sto = (uint)r.GetInt32(0);

				sto = (FieldStorageType)(idx_sto & 0b11);
				sto.DAssert_Defined();

				idx = (int)(idx_sto >> 2);
				Debug.Assert(idx >= 0);
			} else {
				goto NotFound;
			}
		}

		// --

		long rowid;
		string tableName;
		{
			string cmdTxt;
			if (sto != FieldStorageType.Shared) {
				tableName = "Item";
				cmdTxt = "SELECT 1 FROM Item WHERE rowid=$rowid";
				rowid = _RowId;
			} else {
				tableName = "Schema";
				cmdTxt = "SELECT 1 FROM Schema WHERE rowid=$rowid";
				rowid = _SchemaRowId;
			}

			cmd.Reset(cmdTxt);
			cmdParams.Clear();
			cmdParams.Add(new("$rowid", rowid));
		}

		using (var r = cmd.ExecuteReader()) {
			if (r.Read()) {
				// Same as `SqliteDataReader.GetStream()` but more performant
				SqliteBlob blob = new(db, tableName, columnName: "data", rowid, readOnly: true);

				// TODO Batch-load fields in a single fields reader, instead of
				// recreating instances unnecessarily every field load.
				FieldsReader fr;
				if (sto != FieldStorageType.Shared) {
					fr = new ItemFieldsReader(this, blob);
				} else {
					fr = new PlainFieldsReader(this, blob);
				}

				FieldVal fieldVal;
				try {
					fieldVal = fr.ReadFieldVal(idx);
				} finally {
					fr.Dispose();
				}

				// Pending changes will be discarded
				_FieldChanges?.Remove(name);

				SetCache(name, fieldVal);

				return; // Early exit
			}
		}

	NotFound:
		// Otherwise, either deleted or never existed.
		// Let that state materialize here then.
		_FieldChanges?.Remove(name);
		_Fields?.Remove(name);
	}


	public void Unload() {
		UnloadCoreState();
		UnloadFields();
	}

	public void UnloadCoreState() {
		_State = default;
		_Uid = default;
		_ParentRowId = default;
		_Ordinal = default;
		_SchemaRowId = default;
	}

	public void UnloadFields() {
		var fields = _Fields;
		if (fields != null) {
			fields.Clear();
			_FieldChanges = null;
		}
	}


	public static bool RenewRowId(KokoroCollection host, long oldRowId) {
		var context = host.Context;
		var db = host.DbOrNull; Debug.Assert(db != null);
		long newRowId = context.NextItemRowId();
		return AlterRowId(db, oldRowId, newRowId);
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
			updated = db.Cmd("UPDATE Item SET rowid=$newRowId WHERE rowid=$oldRowId")
				.AddParams(new("$oldRowId", oldRowId), new("$newRowId", newRowId))
				.Consume();
		} catch (Exception ex) when (
			ex is not SqliteException sqlex ||
			sqlex.SqliteExtendedErrorCode != SQLitePCL.raw.SQLITE_CONSTRAINT_ROWID
		) {
			db.Context?.UndoItemRowId(newRowId);
			throw;
		}

		Debug.Assert(updated is 1 or 0);
		return ((byte)updated).ToUnsafeBool();
	}


	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool Delete() => DeleteFrom(Host, _RowId);

	public static bool DeleteFrom(KokoroCollection host, long rowid) {
		var db = host.Db;
		int deleted = db.Cmd("DELETE FROM Item WHERE rowid=$rowid")
			.AddParams(new("$rowid", rowid)).Consume();

		Debug.Assert(deleted is 1 or 0);
		return ((byte)deleted).ToUnsafeBool();
	}

	public static bool DeleteFrom(KokoroCollection host, UniqueId uid) {
		int deleted = host.Db.Cmd("DELETE FROM Item WHERE uid=$uid")
			.AddParams(new("$uid", uid)).Consume();

		Debug.Assert(deleted is 1 or 0);
		return ((byte)deleted).ToUnsafeBool();
	}
}
