namespace Kokoro.Internal.Marshal.Fields;
using System.IO;

internal sealed class ColdFieldsReader : BaseFieldsReader<DataEntity> {

	public ColdFieldsReader(DataEntity owner, Stream stream) : base(owner, stream) { }
}
