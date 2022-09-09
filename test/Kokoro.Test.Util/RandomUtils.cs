namespace Kokoro.Test.Util;

public static class RandomUtils {

	public static uint NextUniform32(this Random random) {
		// See, https://stackoverflow.com/a/18332307
		return (uint)random.NextInt64(1L << 32);
	}

	public static ulong NextUniform64(this Random random) {
		// See, https://stackoverflow.com/a/18332307
		return (ulong)((random.NextInt64(1L << 62) << 2) | random.NextInt64(1L << 2));
	}
}
