namespace Kokoro.Common;

internal static class Bytes {

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static unsafe bool ToUnsafeBool(this byte flag) {
		// Reversal of `BoolExtensions.ToByte()` from `CommunityToolkit.HighPerformance`
		// - https://github.com/CommunityToolkit/dotnet/blob/f003c4fc6f93ff280cff5208abf8a54372556049/CommunityToolkit.HighPerformance/Extensions/BoolExtensions.cs#L21
		byte copy = flag;
		return *(bool*)&copy;
	}
}
