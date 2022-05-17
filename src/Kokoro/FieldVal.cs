namespace Kokoro;

public sealed class FieldVal {
	private readonly int _TypeHint;
	private readonly byte[] _Data;

	public FieldVal(int typeHint, byte[] data) {
		Guard.IsGreaterThan(typeHint, 0);
		_TypeHint = typeHint;
		_Data = data;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public FieldVal(FieldTypeHint typeHint, byte[] data)
		: this(typeHint.Value(), data) { }
}
