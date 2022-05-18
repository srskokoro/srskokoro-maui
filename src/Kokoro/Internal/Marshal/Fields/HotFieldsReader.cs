namespace Kokoro.Internal.Marshal.Fields;

internal class HotFieldsReader : FieldsReader2 {

	public HotFieldsReader(Stream stream) : base(stream) { }

	protected sealed override FieldVal? OnReadFieldValFail(int index) => null;
}
