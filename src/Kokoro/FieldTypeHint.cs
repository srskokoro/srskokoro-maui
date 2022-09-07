global using FieldTypeHintInt = System.UInt32;
global using FieldTypeHintUInt = System.UInt32;
global using FieldTypeHintSInt = System.Int32;

namespace Kokoro;
using Kokoro.Common.Util;

public enum FieldTypeHint : FieldTypeHintInt {
	Null     = 0x0,

	/// <summary>The integer zero.</summary>
	Zero     = 0x2,
	/// <summary>The integer one.</summary>
	One      = 0x3,

	/// <summary>
	/// A little-endian arbitrary-length signed integer.
	/// </summary>
	/// <remarks>
	/// Conversion to C#'s primitive integer types may cause the value to be
	/// either truncated or sign-extended. Empty data is interpreted as zero.
	/// </remarks>
	Int      = 0x4,
	/// <summary>
	/// A little-endian arbitrary-length unsigned integer.
	/// </summary>
	/// <remarks>
	/// Conversion to C#'s primitive integer types may cause the value to be
	/// either truncated or zero-extended. Empty data is interpreted as zero.
	/// </remarks>
	UInt     = 0x5,

	/// <summary>
	/// A little-endian IEEE 754 64-bit floating-point number.
	/// </summary>
	/// <remarks>
	/// Excess data bytes will be discarded. Less than 8 bytes (or 64 bits) of
	/// data will be interpreted as zero.
	/// </remarks>
	Real     = 0x8,

	/// <summary>A UTF-8 string.</summary>
	// TODO A zipped UTF-8 counterpart. See, https://utf8everywhere.org/#asian
	Text     = 0x54,

	Blob     = 0x58,

	/// <remarks>
	/// Note: <c>(0x68 + 16) * 2 == 240</c>, which occupies a single byte when
	/// encoded as a <see cref="VarInts">varint</see>.
	/// </remarks>
	StartOfUnreserved = 0x68
}

public static class FieldTypeHintExtensions {

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static FieldTypeHintInt Value(this FieldTypeHint @enum) => (FieldTypeHintInt)@enum;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static bool IsZeroOrOne(this FieldTypeHint @enum) {
		Debug.Assert((FieldTypeHintInt)FieldTypeHint.Zero == 0x2);
		Debug.Assert((FieldTypeHintInt)FieldTypeHint.One == 0x3);
		return ((FieldTypeHintInt)@enum | 1) != 0x3 ? false : true;
		// Ternary operator returning true/false prevents redundant asm generation.
		// See, https://github.com/dotnet/runtime/issues/4207#issuecomment-147184273
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static int GetZeroOrOne(this FieldTypeHint @enum) {
		int r = (FieldTypeHintSInt)@enum & 1;
		Debug.Assert(r == 0
			? @enum == FieldTypeHint.Zero
			: @enum == FieldTypeHint.One);
		return r;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static bool IsIntOrUInt(this FieldTypeHint @enum) {
		Debug.Assert((FieldTypeHintInt)FieldTypeHint.Int == 0x4);
		Debug.Assert((FieldTypeHintInt)FieldTypeHint.UInt == 0x5);
		return ((FieldTypeHintInt)@enum | 1) != 0x5 ? false : true;
		// Ternary operator returning true/false prevents redundant asm generation.
		// See, https://github.com/dotnet/runtime/issues/4207#issuecomment-147184273
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static int WhenIntOrUIntRetM1IfInt(this FieldTypeHint @enum) {
		Debug.Assert(@enum.IsIntOrUInt());
		Debug.Assert((FieldTypeHintInt)FieldTypeHint.Int == 0x4);
		Debug.Assert((FieldTypeHintInt)FieldTypeHint.UInt == 0x5);
		return (FieldTypeHintSInt)(@enum - FieldTypeHint.UInt);
	}
}
