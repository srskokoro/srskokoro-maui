namespace Kokoro.Test.Util;

public class RaceTest {
	private readonly LinkedList<Task> _Tasks = new();
	private object? _ExceptionOrTimeout;
	private readonly int _MinSpinsPerThread;

	// Used to indicate that the test has already timed out and should stop.
	private static readonly object s_TimeoutSignal = new();

	private Timer? _Timer;

	#region Constructors

	public RaceTest(int minSpinsPerThread) {
		_MinSpinsPerThread = minSpinsPerThread;
		_ExceptionOrTimeout = s_TimeoutSignal;
	}

	public RaceTest(int minSpinsPerThread, int millisecondsTimeoutAfterMinSpins) {
		_MinSpinsPerThread = minSpinsPerThread;
		_Timer = new(CreateTimeoutCallback(), this, millisecondsTimeoutAfterMinSpins, Timeout.Infinite);
	}

	public RaceTest(int minSpinsPerThread, TimeSpan timeoutAfterMinSpins) {
		_MinSpinsPerThread = minSpinsPerThread;
		_Timer = new(CreateTimeoutCallback(), this, timeoutAfterMinSpins, Timeout.InfiniteTimeSpan);
	}

	private static TimerCallback CreateTimeoutCallback() => state => {
		var @this = (RaceTest)state!;
		Interlocked.CompareExchange(ref @this._ExceptionOrTimeout, s_TimeoutSignal, null);
		@this._Timer!.Dispose();
		@this._Timer = null;
	};

	#endregion

	public void Wait() {
		Task.WaitAll(_Tasks.ToArray());
		if (_ExceptionOrTimeout is Exception ex)
			ExceptionDispatchInfo.Throw(ex);
	}

	public Exception? WaitNoThrow() {
		Task.WaitAll(_Tasks.ToArray());
		return _ExceptionOrTimeout as Exception;
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
		Exception? exitEx = null;

		try {
			@init();
		} catch (Exception ex) {
			exitEx = ex;
			goto ExitWithException;
		}

		try {
			for (; spins > 0; spins--) {
				if (Volatile.Read(ref race._ExceptionOrTimeout) is Exception) {
					goto Cleanup;
				}
				for (int i = runsPerSpin; i > 0; i--) {
					@body();
				}
			}
			while (Volatile.Read(ref race._ExceptionOrTimeout) is null) {
				for (int i = runsPerSpin; i > 0; i--) {
					@body();
				}
			}
		} catch (Exception ex) {
			exitEx = ex;
		}

	Cleanup:
		try {
			@finally();
		} catch (Exception ex) {
			if (exitEx is not null) {
				exitEx = new AggregateException(exitEx, ex);
				goto ExitWithException;
			}
		}

		if (exitEx is null) {
			return;
		}

	ExitWithException:
		if (Interlocked.CompareExchange(ref race._ExceptionOrTimeout, exitEx, null) is not null) {
			Interlocked.CompareExchange(ref race._ExceptionOrTimeout, exitEx, s_TimeoutSignal);
		}
	}

	private static void DoTaskLoopWithLocal<TLocal>(object? state) {
		var (race, runsPerSpin, @init, @body, @finally) = (Tuple<RaceTest, int, Func<TLocal>, Action<TLocal>, Action<TLocal>>)state!;

		int spins = race._MinSpinsPerThread;
		Exception? exitEx = null;

		TLocal local;
		try {
			local = @init();
		} catch (Exception ex) {
			exitEx = ex;
			goto ExitWithException;
		}

		try {
			for (; spins > 0; spins--) {
				if (Volatile.Read(ref race._ExceptionOrTimeout) is Exception) {
					goto Cleanup;
				}
				for (int i = runsPerSpin; i > 0; i--) {
					@body(local);
				}
			}
			while (Volatile.Read(ref race._ExceptionOrTimeout) is null) {
				for (int i = runsPerSpin; i > 0; i--) {
					@body(local);
				}
			}
		} catch (Exception ex) {
			exitEx = ex;
		}

	Cleanup:
		try {
			@finally(local);
		} catch (Exception ex) {
			if (exitEx is not null) {
				exitEx = new AggregateException(exitEx, ex);
				goto ExitWithException;
			}
		}

		if (exitEx is null) {
			return;
		}

	ExitWithException:
		if (Interlocked.CompareExchange(ref race._ExceptionOrTimeout, exitEx, null) is not null) {
			Interlocked.CompareExchange(ref race._ExceptionOrTimeout, exitEx, s_TimeoutSignal);
		}
	}

	#endregion
}
