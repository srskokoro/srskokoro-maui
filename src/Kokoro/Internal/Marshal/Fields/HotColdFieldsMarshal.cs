namespace Kokoro.Internal.Marshal.Fields;

internal abstract class HotColdFieldsReader : FieldsReader2 {

	public HotColdFieldsReader(Stream hotFieldsData) : base(hotFieldsData) { }

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

	protected sealed override FieldVal? OnReadFieldValFail(int index) {
		Debug.Assert((uint)index >= (uint)_FieldCount);
		return ColdReader.ReadFieldVal(index - _FieldCount);
	}

	public override void Dispose() {
		_ColdReader?.Dispose();
		base.Dispose();
	}
}
