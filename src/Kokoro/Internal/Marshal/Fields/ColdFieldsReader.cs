namespace Kokoro.Internal.Marshal.Fields;
using System.IO;

internal sealed class ColdFieldsReader : BaseFieldsReader {

	public ColdFieldsReader(DataEntity owner, Stream stream) : base(owner, stream) { }
}
