namespace Kokoro.Internal.Marshal.Fields;
using System.IO;

internal abstract class FieldsReader : IDisposable {

	public abstract Stream Stream { get; }

	public abstract int FieldCount { get; }

	// TODO Consider making `Length` an `int` instead?
	public abstract (long Offset, long Length) BoundsOfFieldVal(int index);

	public abstract FieldVal ReadFieldVal(int index);

	public abstract void Dispose();
}
