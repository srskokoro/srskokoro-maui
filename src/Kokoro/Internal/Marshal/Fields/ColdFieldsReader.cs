namespace Kokoro.Internal.Marshal.Fields;

internal class ColdFieldsReader : FieldsReader {

	public ColdFieldsReader(Stream stream) : base(stream) { }

	protected sealed override FieldVal? OnReadFieldValOutOfRange(int index) => null;
}
