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

	// --

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static int CountBytesNeeded(this uint value) {
		// Reference: https://stackoverflow.com/a/2274675
		// See also, https://stackoverflow.com/a/46738575
		return (32 + 7 - BitOperations.LeadingZeroCount(value)) >> 3;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static int CountBytesNeeded(this ulong value) {
		// Reference: https://stackoverflow.com/a/2274675
		// See also, https://stackoverflow.com/a/46738575
		return (64 + 7 - BitOperations.LeadingZeroCount(value)) >> 3;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static int CountBytesNeededOr1(this uint value) {
		int x = value.CountBytesNeeded();
		// NOTE: On both ARM and Intel/AMD machines, the following generates
		// less asm than the equivalent `x | (x == 0)`
		//
		// Assumption: This performs just as well as `x | (x == 0)` in most
		// cases, especially on some machines where `x == 0` requires a branch.
		//
		// TODO Benchmark!
		return x | (int)((uint)(x-1) >> 31);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static int CountBytesNeededOr1(this ulong value) {
		int x = value.CountBytesNeeded();
		// NOTE: On both ARM and Intel/AMD machines, the following generates
		// less asm than the equivalent `x | (x == 0)`
		//
		// Assumption: This performs just as well as `x | (x == 0)` in most
		// cases, especially on some machines where `x == 0` requires a branch.
		//
		// TODO Benchmark!
		return x | (int)((uint)(x-1) >> 63);
	}

	// --

	/// <returns>
	/// <see cref="CountBytesNeeded(uint)"/> - 1
	/// </returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static int CountBytesNeededM1(this uint value) {
		return (32 - 1 - BitOperations.LeadingZeroCount(value)) >> 3;
	}

	/// <returns>
	/// <see cref="CountBytesNeeded(ulong)"/> - 1
	/// </returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static int CountBytesNeededM1(this ulong value) {
		return (64 - 1 - BitOperations.LeadingZeroCount(value)) >> 3;
	}

	/// <returns>
	/// <see cref="CountBytesNeededOr1(uint)"/> - 1
	/// </returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static int CountBytesNeededM1Or0(this uint value) {
		int x = value.CountBytesNeededM1();
		return x ^ (x >> 31);
	}

	/// <returns>
	/// <see cref="CountBytesNeededOr1(ulong)"/> - 1
	/// </returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static int CountBytesNeededM1Or0(this ulong value) {
		int x = value.CountBytesNeededM1();
		return x ^ (x >> 63);
	}
}
