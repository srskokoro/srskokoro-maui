namespace Kokoro.Internal.Marshal.Fields;
using System.IO;

internal abstract class AbsHotColdFieldsReader : BaseFieldsReader.WithModStamps {

	public AbsHotColdFieldsReader(Stream hotFieldsStream) : base(hotFieldsStream) { }

	private protected FieldsReader? _ColdReader;
	public FieldsReader ColdReader {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get {
			var _ = _ColdReader;
			if (_ != null) {
				return _;
			}
			return _ColdReader = ReadColdFieldsData();
		}
	}

	protected virtual FieldsReader ReadColdFieldsData() => NullFieldsReader.Instance;

	protected sealed override FieldVal? OnReadFieldValOutOfRange(int index) {
		Debug.Assert((uint)index >= (uint)FieldCount);
		return ColdReader.ReadFieldVal(index - FieldCount);
	}

	public override void Dispose() {
		_ColdReader?.Dispose();
		base.Dispose();
	}
}
