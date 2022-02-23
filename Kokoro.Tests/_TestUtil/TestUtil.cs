namespace Kokoro.Tests;

public static partial class TestUtil {

#if DEBUG
	public const bool Debug = true;
#else
	public const bool Debug = false;
#endif

	public static Random GetRandom<T>() where T : class, IRandomizedTest
		=> RandomHolder.al_RandomHolder.Value!.GetRandom(typeof(T));

	public static Random GetRandom<T>(this T _)
		where T : class, IRandomizedTest => GetRandom<T>();

	public static Span<byte> Init(this Random random, Span<byte> bytes) {
		random.NextBytes(bytes);
		return bytes;
	}

	public static Span<char> InitAscii(this Random random, Span<char> chars) {
		for (int i = 0; i < chars.Length; i++) {
			chars[i] = (char)random.Next(32, 127);
		}
		return chars;
	}

	public static string MakeAsciiStr(this Random random, int length) {
		return string.Create(length, random, static (chars, random) => random.InitAscii(chars));
	}

	public static string MakeAsciiStr(this Random random, int minLength, int maxLength) {
		return random.MakeAsciiStr(random.Next(minLength, maxLength + 1));
	}
}
