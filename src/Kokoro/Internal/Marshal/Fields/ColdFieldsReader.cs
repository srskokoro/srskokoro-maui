namespace Kokoro.Internal.Marshal.Fields;
using System.IO;

internal class ColdFieldsReader : BaseFieldsReader {

	public ColdFieldsReader(Stream stream) : base(stream) { }

	protected sealed override FieldVal? OnReadFieldValOutOfRange(int index) => null;
}
