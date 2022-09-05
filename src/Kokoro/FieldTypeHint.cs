﻿global using FieldTypeHintInt = System.UInt32;
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

	//Fp8    = 0x6,
	Fp16     = 0x7,
	Fp32     = 0x8,
	Fp64     = 0x9,
	//Fp80   = 0xA,
	//Fp128  = 0xB,
	//Fp256  = 0xC,
	//Dec32  = 0xD,
	//Dec64  = 0xE,
	//Dec128 = 0xF,

	// NOTE: Reserved for integer-with-floating-point hybrid as a custom format.
	// - The idea is to have an arbitrary-precision twos-complement integer (in
	// big-endian) as the significand, prefixed by a length to indicate how many
	// bytes represent the integer part of the number. The length part is either
	// provided as a varint (if the type hint is `NumX`) or indicated by the
	// type hint.
	/*
	Num8     = 0x10,
	Num16    = 0x11,
	Num24    = 0x12,
	Num32    = 0x13,
	Num40    = 0x14,
	Num48    = 0x15,
	Num56    = 0x16,
	Num64    = 0x17,
	NumX     = 0x18,
	 */

	/// <summary>A UTF-8 string.</summary>
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
}
