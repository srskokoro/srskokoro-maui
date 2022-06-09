namespace Kokoro.Internal.Marshal.Fields;
using System.IO;

internal abstract class FieldsReader : IDisposable {

	public abstract Stream Stream { get; }

	public abstract int FieldCount { get; }

	public abstract LatentFieldVal ReadFieldValLater(int index);

	public abstract FieldVal ReadFieldVal(int index);

	public abstract void Dispose();
}
