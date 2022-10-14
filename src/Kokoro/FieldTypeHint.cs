global using FieldTypeHintInt = System.UInt32;
global using FieldTypeHintUInt = System.UInt32;
global using FieldTypeHintSInt = System.Int32;

namespace Kokoro;
using Kokoro.Common.Util;

public enum FieldTypeHint : FieldTypeHintInt {
	Null = 0x0,

	/// <summary>
	/// A little-endian arbitrary-length unsigned integer representing the index
	/// of a field enum under a field enum group in a given schema. Empty data
	/// is interpreted as zero.
	/// </summary>
	Enum = 0x1,

	/// <summary>
	/// A little-endian arbitrary-length negative integer or zero. Empty data is
	/// interpreted as zero.
	/// </summary>
	/// <remarks>
	/// Conversion to C#'s primitive integer types may cause the value to be
	/// either truncated or sign-extended.
	/// </remarks>
	IntNZ = 0x4,

	/// <summary>
	/// A little-endian arbitrary-length positive integer or one. Empty data is
	/// interpreted as one.
	/// </summary>
	/// <remarks>
	/// Conversion to C#'s primitive integer types may cause the value to be
	/// either truncated or zero-extended.
	/// </remarks>
	IntP1 = 0x5,

	/// <summary>
	/// A little-endian IEEE 754 64-bit floating-point number.
	/// </summary>
	/// <remarks>
	/// Excess data bytes are discarded. Data with less than 8 bytes is
	/// interpreted as NaN.
	/// </remarks>
	Real = 0x6,

	/// <summary>A UTF-8 string.</summary>
	// TODO A zipped UTF-8 counterpart. See, https://utf8everywhere.org/#asian
	Text = 0x54,

	Blob = 0x58,

	/// <remarks>
	/// Note: <c>(0x68 + 16) * 2 == 240</c>, which occupies a single byte when
	/// encoded as a <see cref="VarInts">varint</see>.
	/// </remarks>
	StartOfUnreserved = 0x68
}
