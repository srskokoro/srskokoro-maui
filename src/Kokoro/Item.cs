namespace Kokoro;
using Kokoro.Internal;
using Kokoro.Internal.Sqlite;
using Microsoft.Data.Sqlite;

public sealed class Item : FieldedEntity {

	private long _RowId;
	private UniqueId _Uid;

	private long _ParentRowId;
	private int _Ordinal;
	private long _OrdModStamp;

	private long _DataModStamp;

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

		Change_Uid          = 1 << 0,
		Change_ParentRowId  = 1 << 1,
		Change_Ordinal      = 1 << 2,
		Change_OrdModStamp  = 1 << 3,
		Change_SchemaRowId  = 1 << 4,
		Change_DataModStamp = 1 << 5,

		NotExists           = 1 << 31,
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

	public long OrdModStamp {
		get => _OrdModStamp;
		set {
			_OrdModStamp = value;
			_State = StateFlags.Change_OrdModStamp;
		}
	}

	public void SetCachedOrdModStamp(long ordModStamp) => _OrdModStamp = ordModStamp;

	public new long SchemaRowId {
		get => _SchemaRowId;
		set {
			_SchemaRowId = value;
			_State = StateFlags.Change_SchemaRowId;
		}
	}

	public new void SetCachedSchemaRowId(long schemaRowId) => _SchemaRowId = schemaRowId;

	public long DataModStamp {
		get => _DataModStamp;
		set {
			_DataModStamp = value;
			_State = StateFlags.Change_DataModStamp;
		}
	}

	public void SetCachedDataModStamp(long dataModStamp) => _DataModStamp = dataModStamp;


	internal sealed override Stream GetHotData(KokoroSqliteDb db) {
		return new SqliteBlob(db,
			tableName: "Item", columnName: "data",
			rowid: _RowId, readOnly: true);
	}

	// --

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
			SELECT uid,ifnull(parent,0)AS parent,ord,ord_modst,schema,data_modst FROM Item
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
			_OrdModStamp = r.GetInt64(3);

			r.DAssert_Name(4, "schema");
			_SchemaRowId = r.GetInt64(4);

			r.DAssert_Name(5, "data_modst");
			_DataModStamp = r.GetInt64(5);

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
			if (Exists) {
				db.ReloadFieldNameCaches();
				var fr = new FieldsReader(this);
				try {
					InternalLoadField(ref fr, fieldName1);
				} finally {
					fr.Dispose();
				}
			}
		}
	}

	public void Load(StringKey fieldName1, StringKey fieldName2) {
		var db = Host.Db;
		using (new OptionalReadTransaction(db)) {
			Load();
			if (Exists) {
				db.ReloadFieldNameCaches();
				var fr = new FieldsReader(this);
				try {
					InternalLoadField(ref fr, fieldName1);
					InternalLoadField(ref fr, fieldName2);
				} finally {
					fr.Dispose();
				}
			}
		}
	}

	public void Load(StringKey fieldName1, StringKey fieldName2, StringKey fieldName3) {
		var db = Host.Db;
		using (new OptionalReadTransaction(db)) {
			Load();
			if (Exists) {
				db.ReloadFieldNameCaches();
				var fr = new FieldsReader(this);
				try {
					InternalLoadField(ref fr, fieldName1);
					InternalLoadField(ref fr, fieldName2);
					InternalLoadField(ref fr, fieldName3);
				} finally {
					fr.Dispose();
				}
			}
		}
	}

	public void Load(StringKey fieldName1, StringKey fieldName2, StringKey fieldName3, StringKey fieldName4) {
		var db = Host.Db;
		using (new OptionalReadTransaction(db)) {
			Load();
			if (Exists) {
				db.ReloadFieldNameCaches();
				var fr = new FieldsReader(this);
				try {
					InternalLoadField(ref fr, fieldName1);
					InternalLoadField(ref fr, fieldName2);
					InternalLoadField(ref fr, fieldName3);
					InternalLoadField(ref fr, fieldName4);
				} finally {
					fr.Dispose();
				}
			}
		}
	}

	public void Load(StringKey fieldName1, StringKey fieldName2, StringKey fieldName3, StringKey fieldName4, StringKey fieldName5) {
		var db = Host.Db;
		using (new OptionalReadTransaction(db)) {
			Load();
			if (Exists) {
				db.ReloadFieldNameCaches();
				var fr = new FieldsReader(this);
				try {
					InternalLoadField(ref fr, fieldName1);
					InternalLoadField(ref fr, fieldName2);
					InternalLoadField(ref fr, fieldName3);
					InternalLoadField(ref fr, fieldName4);
					InternalLoadField(ref fr, fieldName5);
				} finally {
					fr.Dispose();
				}
			}
		}
	}

	public void Load(StringKey fieldName1, StringKey fieldName2, StringKey fieldName3, StringKey fieldName4, StringKey fieldName5, StringKey fieldName6) {
		var db = Host.Db;
		using (new OptionalReadTransaction(db)) {
			Load();
			if (Exists) {
				db.ReloadFieldNameCaches();
				var fr = new FieldsReader(this);
				try {
					InternalLoadField(ref fr, fieldName1);
					InternalLoadField(ref fr, fieldName2);
					InternalLoadField(ref fr, fieldName3);
					InternalLoadField(ref fr, fieldName4);
					InternalLoadField(ref fr, fieldName5);
					InternalLoadField(ref fr, fieldName6);
				} finally {
					fr.Dispose();
				}
			}
		}
	}

	public void Load(StringKey fieldName1, StringKey fieldName2, StringKey fieldName3, StringKey fieldName4, StringKey fieldName5, StringKey fieldName6, StringKey fieldName7) {
		var db = Host.Db;
		using (new OptionalReadTransaction(db)) {
			Load();
			if (Exists) {
				db.ReloadFieldNameCaches();
				var fr = new FieldsReader(this);
				try {
					InternalLoadField(ref fr, fieldName1);
					InternalLoadField(ref fr, fieldName2);
					InternalLoadField(ref fr, fieldName3);
					InternalLoadField(ref fr, fieldName4);
					InternalLoadField(ref fr, fieldName5);
					InternalLoadField(ref fr, fieldName6);
					InternalLoadField(ref fr, fieldName7);
				} finally {
					fr.Dispose();
				}
			}
		}
	}

	public void Load(StringKey fieldName1, StringKey fieldName2, StringKey fieldName3, StringKey fieldName4, StringKey fieldName5, StringKey fieldName6, StringKey fieldName7, StringKey fieldName8) {
		var db = Host.Db;
		using (new OptionalReadTransaction(db)) {
			Load();
			if (Exists) {
				db.ReloadFieldNameCaches();
				var fr = new FieldsReader(this);
				try {
					InternalLoadField(ref fr, fieldName1);
					InternalLoadField(ref fr, fieldName2);
					InternalLoadField(ref fr, fieldName3);
					InternalLoadField(ref fr, fieldName4);
					InternalLoadField(ref fr, fieldName5);
					InternalLoadField(ref fr, fieldName6);
					InternalLoadField(ref fr, fieldName7);
					InternalLoadField(ref fr, fieldName8);
				} finally {
					fr.Dispose();
				}
			}
			// TODO A counterpart that loads up to 16 fields
			// TODO Generate code via T4 text templates instead
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void Load(params StringKey[] fieldNames)
		=> Load(fieldNames.AsSpan());

	public void Load(ReadOnlySpan<StringKey> fieldNames) {
		var db = Host.Db;
		using (new OptionalReadTransaction(db)) {
			Load();
			if (Exists) {
				db.ReloadFieldNameCaches();
				var fr = new FieldsReader(this);
				try {
					// TODO Unroll?
					foreach (var fieldName in fieldNames)
						InternalLoadField(ref fr, fieldName);
				} finally {
					fr.Dispose();
				}
			}
		}
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

	// --

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
			newRowId = db.Context.NextItemRowId();
			hasUsedNextRowId = true;
		} else {
			hasUsedNextRowId = false;
		}

		int updated;
		try {
			updated = db.Cmd("UPDATE Item SET rowid=$newRowId WHERE rowid=$oldRowId")
				.AddParams(new("$oldRowId", oldRowId), new("$newRowId", newRowId))
				.Consume();
		} catch (Exception ex) when (hasUsedNextRowId && (
			ex is not SqliteException sqlex ||
			sqlex.SqliteExtendedErrorCode != SQLitePCL.raw.SQLITE_CONSTRAINT_ROWID
		)) {
			db.Context?.UndoItemRowId(newRowId);
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
			$"Cannot update `{nameof(Item)}` with rowid {rowid} as it's missing.");
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
		var db = host.Db;
		int deleted = db.Cmd("DELETE FROM Item WHERE uid=$uid")
			.AddParams(new("$uid", uid)).Consume();

		Debug.Assert(deleted is 1 or 0);
		return ((byte)deleted).ToUnsafeBool();
	}
}
