namespace Kokoro;

public sealed class FieldVal {
	private readonly int _TypeHint;
	private readonly byte[] _Data;

	public FieldVal(int typeHint, byte[] data) {
		_TypeHint = typeHint;
		_Data = data;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public FieldVal(FieldTypeHint typeHint, byte[] data)
		: this((int)typeHint, data) { }
}
