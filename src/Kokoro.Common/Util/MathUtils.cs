﻿namespace Kokoro.Common.Util;

internal static class MathUtils {

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static int PosOrZero(this int value) {
		// TODO Replace with `Math.Max(0, value)` once that's optimized.
		// - See, https://github.com/dotnet/runtime/pull/32716#issuecomment-695839706
		return value & ~(value >> 31);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static long PosOrZero(this long value) {
		// TODO Replace with `Math.Max(0, value)` once that's optimized.
		// - See, https://github.com/dotnet/runtime/pull/32716#issuecomment-695839706
		return value & ~(value >> 63);
	}
}
