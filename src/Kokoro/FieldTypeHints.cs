namespace Kokoro;
using Kokoro.Common.Util;

public static class FieldTypeHints {

	/// <remarks>
	/// Note: <c>(0x68 + 16) * 2 == 240</c>, which occupies a single byte when
	/// encoded as a <see cref="VarInts">varint</see>.
	/// </remarks>
	public const FieldTypeHint StartOfUnreserved = (FieldTypeHint)0x68;

	public const FieldTypeHint StartOfReservedForNumeric = FieldTypeHint.IntNZ;
	internal const FieldTypeHint StartOfNumeric = FieldTypeHint.IntNZ;
	internal const FieldTypeHint EndOfNumeric = FieldTypeHint.Real;
	public const FieldTypeHint EndOfReservedForNumeric = (FieldTypeHint)0x1F;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static FieldTypeHintInt Value(this FieldTypeHint @enum) => (FieldTypeHintInt)@enum;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static bool IsInt(this FieldTypeHint @enum) {
		Debug.Assert((FieldTypeHintInt)FieldTypeHint.IntNZ == 0x4);
		Debug.Assert((FieldTypeHintInt)FieldTypeHint.IntP1 == 0x5);
		return ((FieldTypeHintInt)@enum | 1) != 0x5 ? false : true;
		// Ternary operator returning true/false prevents redundant asm generation.
		// See, https://github.com/dotnet/runtime/issues/4207#issuecomment-147184273
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static int WhenIntRetM1IfIntNZ(this FieldTypeHint @enum) {
		Debug.Assert(@enum.IsInt());
		Debug.Assert((FieldTypeHintInt)FieldTypeHint.IntNZ == 0x4);
		Debug.Assert((FieldTypeHintInt)FieldTypeHint.IntP1 == 0x5);
		return (FieldTypeHintSInt)(@enum - FieldTypeHint.IntP1);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static FieldTypeHint IntNZOrP1(int value) {
		Debug.Assert((FieldTypeHintInt)FieldTypeHint.IntNZ == 0x4);
		Debug.Assert((FieldTypeHintInt)FieldTypeHint.IntP1 == 0x5);
		if (U.SizeOf<nint>() < sizeof(long)) {
			// NOTE: The bitwise OR is for when the value is `int.MinValue`
			return (FieldTypeHint)((uint)((value - 1) | value) >> 31) ^ FieldTypeHint.IntP1;
		} else {
			// Optimize if 64-bit arithmetic would cost less
			return (FieldTypeHint)((ulong)(value - 1L) >> 63) ^ FieldTypeHint.IntP1;
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static FieldTypeHint IntNZOrP1(long value) {
		Debug.Assert((FieldTypeHintInt)FieldTypeHint.IntNZ == 0x4);
		Debug.Assert((FieldTypeHintInt)FieldTypeHint.IntP1 == 0x5);
		// NOTE: The bitwise OR is for when the value is `long.MinValue`
		return (FieldTypeHint)((ulong)((value - 1L) | value) >> 63) ^ FieldTypeHint.IntP1;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static FieldTypeHint IntNZOrP1(uint value) {
		Debug.Assert((FieldTypeHintInt)FieldTypeHint.IntNZ == 0x4);
		Debug.Assert((FieldTypeHintInt)FieldTypeHint.IntP1 == 0x5);
		return (FieldTypeHint)((value != 0).ToByte() | 0x4);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static FieldTypeHint IntNZOrP1(ulong value) {
		Debug.Assert((FieldTypeHintInt)FieldTypeHint.IntNZ == 0x4);
		Debug.Assert((FieldTypeHintInt)FieldTypeHint.IntP1 == 0x5);
		return (FieldTypeHint)((value != 0).ToByte() | 0x4);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static bool IsNumeric(this FieldTypeHint @enum) {
		const FieldTypeHintUInt End = EndOfNumeric - StartOfNumeric;
		return (FieldTypeHintUInt)(@enum - StartOfNumeric) > End ? false : true;
		// Ternary operator returning true/false prevents redundant asm generation.
		// See, https://github.com/dotnet/runtime/issues/4207#issuecomment-147184273
	}
}
