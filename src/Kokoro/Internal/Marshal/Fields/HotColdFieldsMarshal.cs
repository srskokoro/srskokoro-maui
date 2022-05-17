namespace Kokoro.Internal.Marshal.Fields;

internal abstract class HotColdFieldsMarshal : FieldsMarshal2 {

	public HotColdFieldsMarshal(Stream hotFieldsData) : base(hotFieldsData) { }

	private protected ColdFieldsMarshal? _ColdReader;
	public ColdFieldsMarshal ColdReader {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get {
			var _ = _ColdReader;
			if (_ != null) {
				return _;
			}
			return _ColdReader = ReadColdFieldsData();
		}
	}

	protected abstract ColdFieldsMarshal ReadColdFieldsData();

	protected sealed override FieldVal? OnReadFieldValFail(int index) {
		Debug.Assert((uint)index >= (uint)_FieldCount);
		return ColdReader.ReadFieldVal(index - _FieldCount);
	}

	public override void Dispose() {
		_ColdReader?.Dispose();
		base.Dispose();
	}
}
