namespace Kokoro;

public static class FieldTypeHints {

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
