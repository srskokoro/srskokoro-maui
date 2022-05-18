namespace Kokoro.Internal.Marshal.Fields;

internal class HotFieldsMarshal : FieldsMarshal2 {

	public HotFieldsMarshal(Stream stream) : base(stream) { }

	protected sealed override FieldVal? OnReadFieldValFail(int index) => null;
}
