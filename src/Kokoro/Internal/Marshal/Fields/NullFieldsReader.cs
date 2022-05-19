namespace Kokoro.Internal.Marshal.Fields;
using System.IO;

internal sealed class NullFieldsReader : FieldsReader {

	public static readonly NullFieldsReader Instance = new();

	private NullFieldsReader() { }

	public override Stream Stream => Stream.Null;

	public override int FieldCount => 0;

	public override FieldVal ReadFieldVal(int index) => FieldVal.Null;

	public override int ModStampCount => 0;

	public override long ReadModStamp(int index)
		=> throw new ArgumentOutOfRangeException(nameof(index));

	public override void Dispose() { }
}
