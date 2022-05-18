namespace Kokoro.Internal.Marshal.Fields;

internal class ColdFieldsReader : FieldsReader {

	public ColdFieldsReader(Stream stream) : base(stream) { }

	protected sealed override FieldVal? OnReadFieldValFail(int index) => null;
}
