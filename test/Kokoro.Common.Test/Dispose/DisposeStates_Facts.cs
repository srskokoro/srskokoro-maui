namespace Kokoro.Common.Dispose;

public class DisposeStates_Facts : IRandomizedTest {
	static Random Random => TestUtil.GetRandom<DisposeStates_Facts>();

	[TestTheory, TestCombinatorialData]
	[TLabel($"Volatile access == non-volatile counterpart")]
	internal void T001(DisposeState state) {
		using (new AssertionCapture()) {
			state.VolatileRead().Should().Be(state);

			state.IsNotDisposed().Should()
				.Be(state.IsNotDisposed_NV());

			state.IsDisposed().Should()
				.Be(state.IsDisposed_NV());

			state.CanHandleDisposeRequest().Should()
				.Be(state.CanHandleDisposeRequest_NV());

			state.CannotHandleDisposeRequest().Should()
				.Be(state.CannotHandleDisposeRequest_NV());

			DisposeState otherState = (DisposeState)((int)state + Random.Next(-0x100, 0x100));
			otherState.VolatileWrite(state);
			otherState.Should().Be(state);
		}
	}

	[TestTheory, TestCombinatorialData(DisableDiscoveryEnumeration = true)]
	[TLabel($"[m!] usage demo")]
	internal void D002_HandleDisposeRequest(DisposeState disposeState, bool disposing) {
		var disposable = new DummyDisposable();
		var oldDisposeState = disposeState;

		RunDemo(); // See demo code below

		disposeState.Should().BeOneOf(
			DisposeState.DisposedPartially,
			DisposeState.DisposedFully,
			oldDisposeState
		);

		// --

		void RunDemo() {
			if (!disposeState.HandleDisposeRequest()) {
				return; // Already disposed or being disposed
			}
			try {
				if (disposing) {
					// Dispose managed state (managed objects).
					//
					// NOTE: If we're here, then we're sure that the constructor
					// completed successfully. Fields that aren't supposed to be
					// null are guaranteed to be non-null, unless we set fields to
					// null only to be called again due to a previous failed
					// dispose attempt.
					// --
					disposable.Dispose();
				}

				// Here we should free unmanaged resources (unmanaged objects),
				// override finalizer, and set large fields to null.
				//
				// NOTE: Make sure to check for null fields, for when the
				// constructor fails to complete or even execute, and the finalizer
				// calls us anyway. See also, https://stackoverflow.com/q/34447080
				// --

				// Mark disposal as successful
				disposeState.CommitDisposeRequest();
			} catch {
				// Failed to dispose everything. Let the next caller of this method
				// continue the disposing operation instead.
				disposeState.RevokeDisposeRequest();
				throw;
			}
		}
	}

	[TestFact]
	[TLabel($"`[m] == true` for only one thread")]
	internal void T003_HandleDisposeRequest() {
		DisposeState state = default;
		int entered = 0;

		using RaceTest race = new();
		race.Queue(-1, 0x100, () => {
			if (!state.HandleDisposeRequest()) {
				return;
			}

			// Only 1 thread should be here at this point.
			Assert.Equal(1, TestUtil.CheckEntry(ref entered));

			// --
			state.RevokeDisposeRequest();
		});
	}

	[TestTheory]
	[TLabel($"[m!] fails when not disposing")]
	[InlineData(DisposeState.None)]
	[InlineData(DisposeState.DisposedPartially)]
	[InlineData(DisposeState.DisposedFully)]
	internal void T004_CommitDisposeRequest(DisposeState state) {
		new Action(() => state.CommitDisposeRequest())
			.Should().Throw<InvalidOperationException>();
	}

	[TestTheory]
	[TLabel($"[m!] NOP when not disposing")]
	[InlineData(DisposeState.None)]
	[InlineData(DisposeState.DisposedPartially)]
	[InlineData(DisposeState.DisposedFully)]
	internal void T005_RevokeDisposeRequest(DisposeState state) {
		using var scope = new AssertionCapture();
		var oldState = state;

		new Action(() => state.RevokeDisposeRequest())
			.Should().NotThrow<InvalidOperationException>();

		state.Should().Be(oldState);
	}

	[TestTheory, TestCombinatorialData(DisableDiscoveryEnumeration = true)]
	[TLabel($"When `[m] == true`, the resulting state is `{nameof(DisposeState.Disposing)}`")]
	internal void T006_HandleDisposeRequest(DisposeState initState) {
		DisposeState state = initState;
		bool stateShouldChange = state.HandleDisposeRequest();

		DisposeState expected = stateShouldChange ? DisposeState.Disposing : initState;
		state.Should().Be(expected, because:
			$"`{nameof(DisposeStates.HandleDisposeRequest)}() == {stateShouldChange.ToKeyword()}`");
	}

	[TestFact]
	[TLabel($"[m!] sets state to `{nameof(DisposeState.DisposedFully)}`")]
	internal void T007_CommitDisposeRequest() {
		DisposeState state = DisposeState.Disposing;
		state.CommitDisposeRequest();
		state.Should().Be(DisposeState.DisposedFully);
	}

	[TestFact]
	[TLabel($"[m!] sets state to `{nameof(DisposeState.DisposedPartially)}`")]
	internal void T008_RevokeDisposeRequest() {
		DisposeState state = DisposeState.Disposing;
		state.RevokeDisposeRequest();
		state.Should().Be(DisposeState.DisposedPartially);
	}
}
