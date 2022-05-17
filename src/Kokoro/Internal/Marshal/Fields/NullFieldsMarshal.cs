namespace Kokoro.Internal.Marshal.Fields;

internal sealed class NullFieldsMarshal : FieldsMarshal2 {
	public static readonly NullFieldsMarshal Instance = new();

	private NullFieldsMarshal() : base(Stream.Null) { }
}
