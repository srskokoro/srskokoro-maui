namespace Kokoro;

public sealed class FieldVal {

	public static FieldVal Null => NullFieldVal.Instance;

	private static class NullFieldVal {
		internal static readonly FieldVal Instance = new();
	}

	private readonly int _TypeHint;
	private readonly byte[] _Data;

	public int IntTypeHint => _TypeHint;

	public FieldTypeHint RawTypeHint => (FieldTypeHint)_TypeHint;

	public FieldTypeHint ResolvedTypeHint {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => RawTypeHint.Resolve();
	}

	public ReadOnlySpan<byte> Data {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => _Data;
	}

	public bool IsInterned {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		// Ternary operator returning true/false prevents redundant asm generation:
		// See, https://github.com/dotnet/runtime/issues/4207#issuecomment-147184273
		get => RawTypeHint.IsInterned() ? true : false;
	}

	public FieldVal() {
		_TypeHint = (int)FieldTypeHint.Null;
		_Data = Array.Empty<byte>();
	}

	public FieldVal(int typeHint, byte[] data) {
		_TypeHint = typeHint;
		_Data = data;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public FieldVal(FieldTypeHint typeHint, byte[] data)
		: this((int)typeHint, data) { }
}
