namespace Kokoro.Internal.Marshal.Fields;
using System.IO;

internal sealed class NullFieldsReader : FieldsReader {

	public static readonly NullFieldsReader Instance = new();

	private NullFieldsReader() { }

	public override Stream Stream => Stream.Null;

	public override FieldVal? ReadFieldVal(int index) => null;

	public override long ReadModStamp(int index)
		=> ThrowHelper.ThrowArgumentOutOfRangeException<long>();

	public override void Dispose() { }
}
