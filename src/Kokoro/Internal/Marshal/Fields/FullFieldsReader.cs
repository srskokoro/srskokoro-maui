namespace Kokoro.Internal.Marshal.Fields;
using System.IO;

internal sealed class FullFieldsReader : BaseFieldsReader<DataEntity>.WithModStamps {

	public FullFieldsReader(DataEntity owner, Stream stream) : base(owner, stream) { }
}
