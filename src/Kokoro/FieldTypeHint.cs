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
	/// data will be interpreted as NaN.
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
