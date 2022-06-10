namespace Kokoro;

public enum FieldTypeHint : int {
	Null     = 0x0,

	/// <summary>The integer zero.</summary>
	Zero     = 0x1,

	/// <summary>The integer one.</summary>
	One      = 0x2,

	/// <summary>
	/// A big-endian arbitrary-length signed integer.
	/// </summary>
	/// <remarks>
	/// Conversion to C#'s primitive integer types may cause the value to be
	/// either truncated or sign-extended.
	/// </remarks>
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
