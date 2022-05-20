namespace Kokoro;
using Kokoro.Internal;

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
}
