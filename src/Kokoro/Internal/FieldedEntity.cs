namespace Kokoro.Internal;
using System.Runtime.InteropServices;

public abstract class FieldedEntity : DataEntity {

	private protected long _SchemaRowId;

	private protected Dictionary<StringKey, FieldVal>? _Fields;
	private protected Dictionary<StringKey, FieldVal>? _FieldChanges;

	public long SchemaRowId => _SchemaRowId;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public FieldedEntity(KokoroCollection host) : base(host) { }

	protected void SetCachedSchemaRowId(long schemaRowId) => _SchemaRowId = schemaRowId;


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
}
