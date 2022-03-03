namespace Kokoro.Test.Util;
using static Kokoro.Test.Framework.IRandomizedTestEstablisher;

public static partial class TestUtil {

#if DEBUG
	public const bool Debug = true;
#else
	public const bool Debug = false;
#endif

	// --

	private class LocalRandomAccess : ILocalRandomAccess {
		public static Random GetRandom<T>() where T : IRandomizedTest
			=> ILocalRandomAccess.GetRandom(typeof(T));
	}

	public static Random GetRandom<T>() where T : class, IRandomizedTest
		=> LocalRandomAccess.GetRandom<T>();

	public static Random GetRandom<T>(this T _) where T : class, IRandomizedTest
		=> LocalRandomAccess.GetRandom<T>();

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

	public static T NextItem<T>(this Random random, T[] array) {
		return array[random.Next(array.Length)];
	}

	public static TEnum NextEnum<TEnum>(this Random random) where TEnum : struct, Enum {
		return random.NextItem(NextEnums<TEnum>.Values);
	}

	private static class NextEnums<TEnum> where TEnum : struct, Enum {
		internal static TEnum[] Values = Enum.GetValues<TEnum>();
	}


	public static void RandomSpin(int minSpinCount, int maxSpinCount) {
		DoSpin(Random.Shared.Next(minSpinCount, maxSpinCount));
	}

	public static void RandomSpin(int maxSpinCount) {
		DoSpin(Random.Shared.Next(maxSpinCount));
	}

	public static void RandomSpin() {
		DoSpin(Random.Shared.Next(8));
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static void DoSpin(int spinCount) {
		if (spinCount > 0) {
			SpinWait spin = default;
			do {
				spin.SpinOnce(-1);
			} while (--spinCount > 0);
		}
	}

	public static void RandomSleep(int minMillisecondsTimeout, int maxMillisecondsTimeout) {
		Thread.Sleep(Random.Shared.Next(minMillisecondsTimeout, maxMillisecondsTimeout));
	}

	public static void RandomSleep(int maxMillisecondsTimeout) {
		Thread.Sleep(Random.Shared.Next(maxMillisecondsTimeout));
	}

	public static void RandomSleep() {
		Thread.Sleep(Random.Shared.Next(4));
	}


	[DoesNotReturn]
	public static void ReThrow(this Exception exception) {
		ExceptionDispatchInfo.Throw(exception);
	}

	/// <summary>
	/// Throws <see cref="Exception.InnerException">InnerException</see> if any.
	/// </summary>
	public static void ReThrowInner(this Exception exception) {
		if (exception.InnerException is Exception inner) {
			ExceptionDispatchInfo.Throw(inner);
		}
	}
}
