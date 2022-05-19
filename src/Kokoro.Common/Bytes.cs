namespace Kokoro.Common;
using System.Numerics;

internal static class Bytes {

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static unsafe bool ToUnsafeBool(this byte flag) {
		// Reversal of `BoolExtensions.ToByte()` from `CommunityToolkit.HighPerformance`
		// - https://github.com/CommunityToolkit/dotnet/blob/f003c4fc6f93ff280cff5208abf8a54372556049/CommunityToolkit.HighPerformance/Extensions/BoolExtensions.cs#L21
		byte copy = flag;
		return *(bool*)&copy;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static int CountBytesNeeded(this uint value)
		=> (32 + 7 - BitOperations.LeadingZeroCount(value)) >> 3;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static int CountBytesNeeded(this ulong value)
		=> (64 + 7 - BitOperations.LeadingZeroCount(value)) >> 3;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static int CountBytesNeededOr1(this uint value) {
		int x = value.CountBytesNeeded();
		// NOTE: On ARM and Intel machines, the following generates less asm
		// than the equivalent `x | (x == 0)`
		return x | (int)((uint)(x-1) >> 31);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static int CountBytesNeededOr1(this ulong value) {
		int x = value.CountBytesNeeded();
		// NOTE: On ARM and Intel machines, the following generates less asm
		// than the equivalent `x | (x == 0)`
		return x | (int)((uint)(x-1) >> 63);
	}
}
