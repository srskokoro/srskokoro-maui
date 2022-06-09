namespace Kokoro.Internal.Marshal.Fields;
using System.IO;

internal sealed class NullFieldsReader : FieldsReader {

	public static readonly NullFieldsReader Instance = new();

	private NullFieldsReader() { }

	public override Stream Stream => Stream.Null;

	public override int FieldCount => 0;

	public override LatentFieldVal ReadFieldValLater(int index) => LatentFieldVal.Null;

	public override FieldVal ReadFieldVal(int index) => FieldVal.Null;

	public override void Dispose() { }
}
