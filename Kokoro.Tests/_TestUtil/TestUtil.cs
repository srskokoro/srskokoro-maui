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


	public static void RandomSpin(int minSpinCount, int maxSpinCount) {
		DoSpin(Random.Shared.Next(minSpinCount, maxSpinCount));
	}

	public static void RandomSpin(int maxSpinCount) {
		DoSpin(Random.Shared.Next(maxSpinCount));
	}

	public static void RandomSpin() {
		DoSpin(Random.Shared.Next(0x100));
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static void DoSpin(int spinCount) {
		for (int i = 0; i < spinCount; i++) {
			if (((i & 0x7) ^ 0x7) == 0) {
				if (((i & 0x3F) ^ 0x3F) == 0) {
					Thread.Sleep(0);
				}
				Thread.Yield();
			}
		}
	}

	public static void RandomSleep(int minMillisecondsTimeout, int maxMillisecondsTimeout) {
		Thread.Sleep(Random.Shared.Next(minMillisecondsTimeout, maxMillisecondsTimeout));
	}

	public static void RandomSleep(int maxMillisecondsTimeout) {
		Thread.Sleep(Random.Shared.Next(maxMillisecondsTimeout));
	}


	[StackTraceHidden]
	[DoesNotReturn]
	public static void Throw(this Exception exception) {
		ExceptionDispatchInfo.Throw(exception);
	}

	/// <summary>
	/// Throws <see cref="Exception.InnerException">InnerException</see> if any.
	/// </summary>
	[StackTraceHidden]
	public static void ThrowInner(this Exception exception) {
		if (exception.InnerException is Exception inner) {
			ExceptionDispatchInfo.Throw(inner);
		}
	}
}
