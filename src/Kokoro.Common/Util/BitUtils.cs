namespace Kokoro.Common.Util;

internal static class BitUtils {

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static int NonNegOrNegate(this int value) {
		return value ^ (value >> 31);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static long NonNegOrNegate(this long value) {
		return value ^ (value >> 63);
	}
}
