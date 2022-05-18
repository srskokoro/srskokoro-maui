namespace Kokoro.Internal.Marshal.Fields;

internal sealed class NullFieldsReader : FieldsReader2 {
	public static readonly NullFieldsReader Instance = new();

	private NullFieldsReader() => _Stream = Stream.Null;
}
