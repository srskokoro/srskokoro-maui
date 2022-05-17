namespace Kokoro.Internal.Fields.Readers;

internal abstract class HotColdFieldsDataReader : FieldsDataReader2 {

	public HotColdFieldsDataReader(Stream hotFieldsData) : base(hotFieldsData) { }

	private protected ColdFieldsDataReader? _ColdReader;
	public ColdFieldsDataReader ColdReader {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get {
			var _ = _ColdReader;
			if (_ != null) {
				return _;
			}
			return _ColdReader = ReadColdFieldsData();
		}
	}

	protected abstract ColdFieldsDataReader ReadColdFieldsData();

	protected sealed override FieldVal OnReadFieldValFail(int index) {
		Debug.Assert((uint)index >= (uint)_FieldCount);
		return ColdReader.ReadFieldVal(index - _FieldCount);
	}

	public override void Dispose() {
		_ColdReader?.Dispose();
		base.Dispose();
	}
}
