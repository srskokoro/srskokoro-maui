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

	// --

	public static Span<byte> Init(this Random random, Span<byte> bytes) {
		random.NextBytes(bytes);
		return bytes;
	}

	public static Span<char> InitAscii(this Random random, Span<char> chars) {
		for (int i = 0; i < chars.Length; i++) {
			chars[i] = random.NextAscii();
		}
		return chars;
	}

	public static string MakeAsciiStr(this Random random, int length) {
		return string.Create(length, random, static (chars, random) => random.InitAscii(chars));
	}

	public static string MakeAsciiStr(this Random random, int minLength, int maxLength) {
		return random.MakeAsciiStr(random.Next(minLength, maxLength + 1));
	}

	public static char NextAscii(this Random random) {
		return (char)random.Next(32, 127);
	}

	// --

	public static T NextItem<T>(this Random random, T[] array) {
		return array[random.Next(array.Length)];
	}

	public static TEnum NextEnum<TEnum>(this Random random) where TEnum : struct, Enum {
		return random.NextItem(EnumUtils.EnumVals<TEnum>.Values);
	}
}
