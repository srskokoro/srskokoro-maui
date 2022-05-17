namespace Kokoro.Internal.Marshal.Fields;

internal sealed class NullFieldsMarshal : FieldsMarshal2 {

	public NullFieldsMarshal() : base(Stream.Null) { }
}
