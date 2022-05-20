namespace Kokoro;
using Kokoro.Internal;
using Kokoro.Internal.Sqlite;
using Microsoft.Data.Sqlite;

public sealed class Item : DataEntity {

	private long _RowId;
	private UniqueId _Uid;

	private long _ParentRowId;
	private int _Ordinal;

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
		Change_SchemaRowId = 1 << 3,

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
		return;

	Init:
		_Fields = fields = new();
		goto Set;
	}


	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static long LoadRowId(KokoroCollection host, UniqueId uid)
		=> LoadRowId(host.Db, uid);

	internal static long LoadRowId(KokoroSqliteDb db, UniqueId uid)
		=> db.ExecScalar<long>(
			"SELECT rowid FROM Items WHERE uid=$uid"
			, new SqliteParameter("$uid", uid.ToByteArray()));


	public static bool RenewRowId(KokoroCollection host, long oldRowId) {
		var context = host.Context;
		long newRowId = context.NextItemRowId();
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
				"UPDATE Items SET rowid=$newRowId WHERE rowid=$oldRowId"
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
}
