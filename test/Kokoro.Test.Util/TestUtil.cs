namespace Kokoro.Test.Util;
using static Kokoro.Test.Util.Framework.IRandomizedTestEstablisher;

public static partial class TestUtil {

	private class LocalRandomAccess : ILocalRandomAccess {
		public static Random GetRandom<T>() where T : IRandomizedTest
			=> ILocalRandomAccess.GetRandom(typeof(T));
	}

	public static Random GetRandom<T>() where T : class, IRandomizedTest
		=> LocalRandomAccess.GetRandom<T>();

	public static Random GetRandom<T>(this T _) where T : class, IRandomizedTest
		=> LocalRandomAccess.GetRandom<T>();


	public static void RandomSpin(int minSpinCount, int maxSpinCount) {
		DoSpin(Random.Shared.Next(minSpinCount, maxSpinCount));
	}

	public static void RandomSpin(int maxSpinCount) {
		DoSpin(Random.Shared.Next(maxSpinCount));
	}

	public static void RandomSpin() {
		DoSpin(Random.Shared.Next(128));
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
		Thread.Sleep(Random.Shared.Next(8));
	}

	public static void BarrierYield() {
		// See also, https://stackoverflow.com/questions/6581848/memory-barrier-generators
		Thread.Sleep(0);
		Interlocked.MemoryBarrier();
	}

	public static void BarrierYieldSpin() {
		BarrierYield();
		RandomSpin();
	}

	public static void BarrierYieldSpin(int spinCount) {
		BarrierYield();
		RandomSpin(spinCount);
	}

	public static int CheckEntry(ref int entered) {
		int current = Interlocked.Increment(ref entered);
		Interlocked.Decrement(ref entered);
		return current;
	}
}
