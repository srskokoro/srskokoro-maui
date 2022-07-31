namespace Kokoro.Common.Util;

internal static class TimeUtils {

	/// <summary>
	/// The Unix epoch (1970-01-01 00:00 UTC) as the number of milliseconds
	/// since the BCL epoch (0001-01-01 00:00 UTC).
	/// </summary>
	private const long UnixEpochMilliseconds = 62_135_596_800_000;

	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	[SkipLocalsInit]
	public static long UnixMillisNow() {
		return DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond - UnixEpochMilliseconds;
		//
		// Explanation from `DateTimeOffset.ToUnixTimeMilliseconds()`:
		//
		//   > Truncate sub-millisecond precision before offsetting by the Unix
		//   > epoch to avoid the last digit being off by one for dates that
		//   > result in negative Unix times.
		//
		// https://github.com/dotnet/runtime/blob/v6.0.7/src/libraries/System.Private.CoreLib/src/System/DateTimeOffset.cs#L629
	}
}
