namespace Kokoro.Internal.Marshal.Fields;
using System.IO;

internal sealed class NullFieldsReader : FieldsReader {

	public static readonly NullFieldsReader Instance = new();

	private NullFieldsReader() { }

	public override Stream Stream => Stream.Null;

	public override int FieldCount => 0;

	public override (long Offset, long Length) BoundsOfFieldVal(int index) => default;

	public override FieldVal ReadFieldVal(int index) => FieldVal.Null;

	public override void Dispose() { }
}
