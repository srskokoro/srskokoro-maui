namespace Kokoro;
using Kokoro.Common.Util;

public enum FieldTypeHint : int {
	Null     = 0x0,

	/// <summary>The integer zero.</summary>
	Zero     = 0x2,
	/// <summary>The integer one.</summary>
	One      = 0x3,

	/// <summary>
	/// A big-endian arbitrary-length signed integer.
	/// </summary>
	/// <remarks>
	/// Conversion to C#'s primitive integer types may cause the value to be
	/// either truncated or sign-extended.
	/// </remarks>
	Int      = 0x4,
	/// <summary>
	/// A big-endian arbitrary-length unsigned integer.
	/// </summary>
	/// <remarks>
	/// Conversion to C#'s primitive integer types may cause the value to be
	/// either truncated or zero-extended.
	/// </remarks>
	UInt     = 0x5,

	Text     = 0x57,
	Blob     = 0x58,

	/// <remarks>
	/// Note: <c>(0x68 + 16) * 2 == 240</c>, which occupies a single byte when
	/// encoded as a <see cref="VarInts">varint</see>.
	/// </remarks>
	StartOfUnreserved = 0x68
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
