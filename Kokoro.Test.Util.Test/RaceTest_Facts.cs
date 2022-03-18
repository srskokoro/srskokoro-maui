namespace Kokoro.Test.Util;
using Xunit.Sdk;

public class RaceTest_Facts {

	[TestFact]
	[TLabel($"Fails on simple 'check-then-act' race condition")]
	public void T001() {
		new Action(() => {
			int barrier = 0;
			int entered = 0;
			using RaceTest race = new();
			race.Queue(-1, 0x100, () => {
				if (barrier != 0) {
					return;
				}
				barrier = 1;
				Interlocked.MemoryBarrier();

				// Only 1 thread should be here at this point.
				Assert.Equal(1, TestUtil.CheckEntry(ref entered));

				barrier = 0;
			});
		}).Should().Throw<XunitException>();
	}

	[TestFact]
	[TLabel($"Succeeds on properly implemented 'check-then-act' operation")]
	public void T002() {
		new Action(() => {
			int barrier = 0;
			int entered = 0;
			using RaceTest race = new();
			race.Queue(-1, 0x100, () => {
				if (Interlocked.CompareExchange(ref barrier, 1, 0) != 0) {
					return;
				}

				// Only 1 thread should be here at this point.
				Assert.Equal(1, TestUtil.CheckEntry(ref entered));

				barrier = 0;
			});
		}).Should().NotThrow();
	}

	[TestFact]
	[TLabel($"Fails quickly after a thread throws")]
	public void T003() {
		using var scope = new AssertionCapture();

		const int RunsPerSpins = 0x100;
		int NonThrowingThreads = Environment.ProcessorCount;
		int i = 0;

		new Action(() => {
			using RaceTest race = new(-1, 2000);
			race.Queue(1 + NonThrowingThreads, RunsPerSpins, () => {
				if (Interlocked.Increment(ref i) == 1) {
					throw new Exception();
				} else {
					Thread.Sleep(0);
				}
			});
		}).Should().Throw<Exception>();

		int ExpectedIncrements = RunsPerSpins * NonThrowingThreads + 1;
		i.Should().BeLessThanOrEqualTo(ExpectedIncrements);
	}
}
