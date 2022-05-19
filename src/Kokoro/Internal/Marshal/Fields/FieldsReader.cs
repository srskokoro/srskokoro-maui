namespace Kokoro.Internal.Marshal.Fields;
using System.IO;

internal abstract class FieldsReader : IDisposable {

	public abstract Stream Stream { get; }

	public abstract FieldVal ReadFieldVal(int index);

	public abstract long ReadModStamp(int index);

	public abstract void Dispose();
}
