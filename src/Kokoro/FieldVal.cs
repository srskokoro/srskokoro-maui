namespace Kokoro;

public sealed class FieldVal {

	public static FieldVal Null => NullInstHolder.Instance;

	private static class NullInstHolder {
		internal static readonly FieldVal Instance = new();
	}

	private readonly uint _TypeHint;
	private readonly byte[] _Data;

	public FieldTypeHint TypeHint => (FieldTypeHint)_TypeHint;

	public ReadOnlySpan<byte> Data {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => _Data;
	}

	public FieldVal() {
		_TypeHint = (uint)FieldTypeHint.Null;
		_Data = Array.Empty<byte>();
	}

	public FieldVal(uint typeHint, byte[] data) {
		_TypeHint = typeHint;
		_Data = data;
	}

	public FieldVal(FieldTypeHint typeHint, byte[] data) {
		_TypeHint = (uint)typeHint;
		_Data = data;
	}
}
