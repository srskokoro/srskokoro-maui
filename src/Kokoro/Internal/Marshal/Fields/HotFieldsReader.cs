namespace Kokoro.Internal.Marshal.Fields;
using System.IO;

internal sealed class HotFieldsReader : BaseFieldsReader.WithModStamps {

	public HotFieldsReader(Stream stream) : base(stream) { }
}
