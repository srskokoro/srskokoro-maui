namespace Kokoro.Internal.Marshal.Fields;

internal class ColdFieldsMarshal : FieldsMarshal {

	public ColdFieldsMarshal(Stream stream) : base(stream) { }

	protected sealed override FieldVal? OnReadFieldValFail(int index) => null;
}
