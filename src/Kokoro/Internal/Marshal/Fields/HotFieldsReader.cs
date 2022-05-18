namespace Kokoro.Internal.Marshal.Fields;
using System.IO;

internal sealed class HotFieldsReader : BaseFieldsReader<DataEntity>.WithModStamps {

	public HotFieldsReader(DataEntity owner, Stream stream) : base(owner, stream) { }
}
