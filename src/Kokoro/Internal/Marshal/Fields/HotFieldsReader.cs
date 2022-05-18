namespace Kokoro.Internal.Marshal.Fields;
using System.IO;

internal class HotFieldsReader : BaseFieldsReader.WithModStamps {

	public HotFieldsReader(Stream stream) : base(stream) { }

	protected sealed override FieldVal? OnReadFieldValOutOfRange(int index) => null;
}
