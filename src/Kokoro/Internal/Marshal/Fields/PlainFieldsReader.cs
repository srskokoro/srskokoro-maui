namespace Kokoro.Internal.Marshal.Fields;
using System.IO;

internal sealed class PlainFieldsReader : BaseFieldsReader<DataEntity> {

	public PlainFieldsReader(DataEntity owner, Stream stream) : base(owner, stream) { }
}
