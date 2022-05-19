namespace Kokoro.Internal.Marshal.Fields;
using System.IO;

internal abstract class AbsHotColdFieldsReader<TOwner> : BaseFieldsReader<TOwner>.WithModStamps
		where TOwner : DataEntity {

	public AbsHotColdFieldsReader(TOwner owner, Stream hotFieldsStream)
		: base(owner, hotFieldsStream) { }

	private FieldsReader? _ColdReader;
	public FieldsReader ColdReader {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get {
			var _ = _ColdReader;
			if (_ != null) {
				return _;
			}
			return _ColdReader = CreateColdFieldsReader();
		}
	}

	protected abstract FieldsReader CreateColdFieldsReader();

	protected sealed override FieldVal OnReadFieldValOutOfRange(int index) {
		Debug.Assert((uint)index >= (uint)FieldCount);
		return ColdReader.ReadFieldVal(index - FieldCount);
	}

	public override void Dispose() {
		_ColdReader?.Dispose();
		base.Dispose();
	}
}
