namespace Kokoro.Tests;

public class RaceTest {
	private readonly LinkedList<Task> _Tasks = new();
	private readonly CancellationToken _EndToken;
	private readonly int _MinSpinsPerThread;

	#region Constructors

	public RaceTest(int minSpinsPerThread) {
		_MinSpinsPerThread = minSpinsPerThread;
		_EndToken = new CancellationToken(true);
	}

	public RaceTest(int minSpinsPerThread, int millisecondsTimeoutAfterMinSpins) {
		_MinSpinsPerThread = minSpinsPerThread;
		_EndToken = new CancellationTokenSource(millisecondsTimeoutAfterMinSpins).Token;
	}

	public RaceTest(int minSpinsPerThread, TimeSpan timeoutAfterMinSpins) {
		_MinSpinsPerThread = minSpinsPerThread;
		_EndToken = new CancellationTokenSource(timeoutAfterMinSpins).Token;
	}

	#endregion

	/// <exception cref="RaceAggregateException"></exception>
	public void Wait() {
		try {
			Task.WaitAll(_Tasks.ToArray());
		} catch (AggregateException ex) {
			throw new RaceAggregateException(ex.InnerExceptions);
		}
	}

	public RaceAggregateException? WaitNoThrow() {
		try {
			Task.WaitAll(_Tasks.ToArray());
		} catch (AggregateException ex) {
			return new RaceAggregateException(ex.InnerExceptions);
		}
		return null;
	}

	#region Defaults

	private static readonly int DefaultNumThreads = Math.Max(Environment.ProcessorCount * 2, 4);
	private const int DefaultRunsPerSpin = 8;

	private static readonly Action DefaultAction = () => { };

	private static class Default<T> {
		public static readonly Func<T> Func = () => default!;
		public static readonly Action<T> Action = _ => { };
	}

	#endregion

	#region Convenient Overloads

	#region Without `TLocal`

	public RaceTest Queue(Action @body)
		=> Queue(DefaultNumThreads, DefaultRunsPerSpin, DefaultAction, @body, DefaultAction);

	public RaceTest Queue(Action @init, Action @body)
		=> Queue(DefaultNumThreads, DefaultRunsPerSpin, @init, @body, DefaultAction);

	public RaceTest Queue(Action @init, Action @body, Action @finally)
		=> Queue(DefaultNumThreads, DefaultRunsPerSpin, @init, @body, @finally);


	public RaceTest Queue(int numThreads, Action @body)
		=> Queue(numThreads, DefaultRunsPerSpin, DefaultAction, @body, DefaultAction);

	public RaceTest Queue(int numThreads, Action @init, Action @body)
		=> Queue(numThreads, DefaultRunsPerSpin, @init, @body, DefaultAction);

	public RaceTest Queue(int numThreads, Action @init, Action @body, Action @finally)
		=> Queue(numThreads, DefaultRunsPerSpin, @init, @body, @finally);


	public RaceTest Queue(int numThreads, int runsPerSpin, Action @body)
		=> Queue(numThreads, runsPerSpin, DefaultAction, @body, DefaultAction);

	public RaceTest Queue(int numThreads, int runsPerSpin, Action @init, Action @body)
		=> Queue(numThreads, runsPerSpin, @init, @body, DefaultAction);

	#endregion

	#region With `TLocal`

	public RaceTest Queue<TLocal>(Action<TLocal> @body)
		=> Queue(DefaultNumThreads, DefaultRunsPerSpin, Default<TLocal>.Func, @body, Default<TLocal>.Action);

	public RaceTest Queue<TLocal>(Func<TLocal> @init, Action<TLocal> @body)
		=> Queue(DefaultNumThreads, DefaultRunsPerSpin, @init, @body, Default<TLocal>.Action);

	public RaceTest Queue<TLocal>(Func<TLocal> @init, Action<TLocal> @body, Action<TLocal> @finally)
		=> Queue(DefaultNumThreads, DefaultRunsPerSpin, @init, @body, @finally);


	public RaceTest Queue<TLocal>(int numThreads, Action<TLocal> @body)
		=> Queue(numThreads, DefaultRunsPerSpin, Default<TLocal>.Func, @body, Default<TLocal>.Action);

	public RaceTest Queue<TLocal>(int numThreads, Func<TLocal> @init, Action<TLocal> @body)
		=> Queue(numThreads, DefaultRunsPerSpin, @init, @body, Default<TLocal>.Action);

	public RaceTest Queue<TLocal>(int numThreads, Func<TLocal> @init, Action<TLocal> @body, Action<TLocal> @finally)
		=> Queue(numThreads, DefaultRunsPerSpin, @init, @body, @finally);


	public RaceTest Queue<TLocal>(int numThreads, int runsPerSpin, Action<TLocal> @body)
		=> Queue(numThreads, runsPerSpin, Default<TLocal>.Func, @body, Default<TLocal>.Action);

	public RaceTest Queue<TLocal>(int numThreads, int runsPerSpin, Func<TLocal> @init, Action<TLocal> @body)
		=> Queue(numThreads, runsPerSpin, @init, @body, Default<TLocal>.Action);

	#endregion

	#region With `object?`

	public RaceTest Queue(Action<object?> @body)
		=> Queue<object?>(DefaultNumThreads, DefaultRunsPerSpin, Default<object?>.Func, @body, Default<object?>.Action);

	public RaceTest Queue(Func<object?> @init, Action<object?> @body)
		=> Queue<object?>(DefaultNumThreads, DefaultRunsPerSpin, @init, @body, Default<object?>.Action);

	public RaceTest Queue(Func<object?> @init, Action<object?> @body, Action<object?> @finally)
		=> Queue<object?>(DefaultNumThreads, DefaultRunsPerSpin, @init, @body, @finally);


	public RaceTest Queue(int numThreads, Action<object?> @body)
		=> Queue<object?>(numThreads, DefaultRunsPerSpin, Default<object?>.Func, @body, Default<object?>.Action);

	public RaceTest Queue(int numThreads, Func<object?> @init, Action<object?> @body)
		=> Queue<object?>(numThreads, DefaultRunsPerSpin, @init, @body, Default<object?>.Action);

	public RaceTest Queue(int numThreads, Func<object?> @init, Action<object?> @body, Action<object?> @finally)
		=> Queue<object?>(numThreads, DefaultRunsPerSpin, @init, @body, @finally);


	public RaceTest Queue(int numThreads, int runsPerSpin, Action<object?> @body)
		=> Queue<object?>(numThreads, runsPerSpin, Default<object?>.Func, @body, Default<object?>.Action);

	public RaceTest Queue(int numThreads, int runsPerSpin, Func<object?> @init, Action<object?> @body)
		=> Queue<object?>(numThreads, runsPerSpin, @init, @body, Default<object?>.Action);

	public RaceTest Queue(int numThreads, int runsPerSpin, Func<object?> @init, Action<object?> @body, Action<object?> @finally)
		=> Queue<object?>(numThreads, runsPerSpin, @init, @body, @finally);

	#endregion

	#endregion

	#region Main implementation

	private const TaskCreationOptions CommonTaskCreationOptions
			= TaskCreationOptions.LongRunning
			| TaskCreationOptions.HideScheduler
			| TaskCreationOptions.DenyChildAttach;

	public RaceTest Queue(int numThreads, int runsPerSpin, Action @init, Action @body, Action @finally) {
		if (numThreads < 0) numThreads = DefaultNumThreads;
		if (runsPerSpin < 0) throw new ArgumentOutOfRangeException(nameof(runsPerSpin));

		Tuple<RaceTest, int, Action, Action, Action> state
			= new(this, runsPerSpin, @init, @body, @finally);

		for (; numThreads > 0; numThreads--) {
			var task = Task.Factory.StartNew(
				DoTaskLoopWithoutLocal, state,
				CancellationToken.None,
				CommonTaskCreationOptions,
				TaskScheduler.Default
			);
			_Tasks.AddLast(task);
		}

		return this;
	}

	public RaceTest Queue<TLocal>(int numThreads, int runsPerSpin, Func<TLocal> @init, Action<TLocal> @body, Action<TLocal> @finally) {
		if (numThreads < 0) numThreads = DefaultNumThreads;
		if (runsPerSpin < 0) throw new ArgumentOutOfRangeException(nameof(runsPerSpin));

		Tuple<RaceTest, int, Func<TLocal>, Action<TLocal>, Action<TLocal>> state
			= new(this, runsPerSpin, @init, @body, @finally);

		for (; numThreads > 0; numThreads--) {
			var task = Task.Factory.StartNew(
				DoTaskLoopWithLocal<TLocal>, state,
				CancellationToken.None,
				CommonTaskCreationOptions,
				TaskScheduler.Default
			);
			_Tasks.AddLast(task);
		}

		return this;
	}

	private static void DoTaskLoopWithoutLocal(object? state) {
		var (race, runsPerSpin, @init, @body, @finally) = (Tuple<RaceTest, int, Action, Action, Action>)state!;

		int spins = race._MinSpinsPerThread;
		var et = race._EndToken;

		@init();

		Exception? mainEx = null;
		try {
			for (; spins > 0; spins--) {
				for (int i = runsPerSpin; i > 0; i--) {
					@body();
				}
			}
			while (!et.IsCancellationRequested) {
				for (int i = runsPerSpin; i > 0; i--) {
					@body();
				}
			}
		} catch (Exception ex) {
			mainEx = ex;
		}

		try {
			@finally();
		} catch (Exception ex) {
			if (mainEx != null) {
				throw new AggregateException(mainEx, ex);
			}
			throw;
		}

		if (mainEx != null) {
			ExceptionDispatchInfo.Throw(mainEx);
		}
	}

	private static void DoTaskLoopWithLocal<TLocal>(object? state) {
		var (race, runsPerSpin, @init, @body, @finally) = (Tuple<RaceTest, int, Func<TLocal>, Action<TLocal>, Action<TLocal>>)state!;

		int spins = race._MinSpinsPerThread;
		var et = race._EndToken;

		TLocal local = @init();

		Exception? mainEx = null;
		try {
			for (; spins > 0; spins--) {
				for (int i = runsPerSpin; i > 0; i--) {
					@body(local);
				}
			}
			while (!et.IsCancellationRequested) {
				for (int i = runsPerSpin; i > 0; i--) {
					@body(local);
				}
			}
		} catch (Exception ex) {
			mainEx = ex;
		}

		try {
			@finally(local);
		} catch (Exception ex) {
			if (mainEx != null) {
				throw new AggregateException(mainEx, ex);
			}
			throw;
		}

		if (mainEx != null) {
			ExceptionDispatchInfo.Throw(mainEx);
		}
	}

	#endregion
}
