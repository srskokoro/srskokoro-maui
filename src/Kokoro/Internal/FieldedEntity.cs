namespace Kokoro.Internal;

public abstract class FieldedEntity : DataEntity {

	private protected long _SchemaRowId;

	public long SchemaRowId => _SchemaRowId;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public FieldedEntity(KokoroCollection host) : base(host) { }

	protected void SetCachedSchemaRowId(long schemaRowId) => _SchemaRowId = schemaRowId;
}
