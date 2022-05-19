namespace Kokoro.Internal.Marshal.Fields;
using System.IO;

internal abstract class FieldsReader : IDisposable {

	public abstract Stream Stream { get; }

	public abstract int FieldCount { get; }

	public abstract FieldVal ReadFieldVal(int index);

	public virtual int ModStampCount => 0;

	public virtual long ReadModStamp(int index)
		=> throw new ArgumentOutOfRangeException(nameof(index));

	public abstract void Dispose();
}
