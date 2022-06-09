namespace Kokoro;

public enum FieldTypeHint : int {
	Null     = 0x0,

	/// <summary>The integer zero.</summary>
	Zero     = 0x1,

	/// <summary>The integer one.</summary>
	One      = 0x2,

	/// <summary>
	/// A two's complement integer occupying zero or more bytes, up to 8 bytes.
	/// When zero bytes, this is the same as the integer zero.
	/// </summary>
	Int      = 0x3,

	Text     = 0x3E,
	Blob     = 0x3F,
}

public static class FieldTypeHintExtensions {

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static int Value(this FieldTypeHint @enum) => (int)@enum;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsInterned(this FieldTypeHint @enum) {
		// Ternary operator returning true/false prevents redundant asm generation:
		// See, https://github.com/dotnet/runtime/issues/4207#issuecomment-147184273
		return @enum < 0 ? true : false;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static FieldTypeHint ToggleInterned(this FieldTypeHint @enum) => ~@enum;

	/// <summary>
	/// Resolves to the actual non-interned counterpart (if currently interned).
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static FieldTypeHint Resolve(this FieldTypeHint @enum) {
		int copy = (int)@enum;
		return (FieldTypeHint)(copy ^ (copy >> (sizeof(FieldTypeHint) * 8 - 1)));
	}
}
