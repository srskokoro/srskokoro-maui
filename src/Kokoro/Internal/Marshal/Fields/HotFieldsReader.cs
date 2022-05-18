namespace Kokoro.Internal.Marshal.Fields;
using System.IO;

internal sealed class HotFieldsReader : BaseFieldsReader.WithModStamps {

	public HotFieldsReader(DataEntity owner, Stream stream) : base(owner, stream) { }
}
