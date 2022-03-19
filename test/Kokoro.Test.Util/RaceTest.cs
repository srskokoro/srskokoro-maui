namespace Kokoro.Test.Util;

public class RaceTest : IDisposable, IAsyncDisposable {
	private LinkedList<TaskItem>? _TaskItems = new();
	private object? _ExceptionOrTimeout;
	private readonly int _MinSpinsPerThread;
	private readonly int _MinRunMillis;

	private readonly ManualResetEventSlim _StartEvent = new();
	private Timer? _Timer;

	#region Constructor

	private const int DefaultMinSpinsPerThread = 8;
	private const int DefaultMinRunMillis = 150;

	public RaceTest(int minSpinsPerThread = DefaultMinSpinsPerThread, int minRunMillis = DefaultMinRunMillis) {
		_MinSpinsPerThread = minSpinsPerThread < 0 ? DefaultMinSpinsPerThread : minSpinsPerThread;
		_MinRunMillis = minRunMillis < 0 ? DefaultMinRunMillis : minRunMillis;
	}

	#endregion

	#region Internal Setup

	// Used to indicate that the test has already timed out and should stop as
	// early as it can.
	private static readonly object s_TimeoutSignal = new();

	private static readonly TimerCallback s_TimeoutCallback = state => {
		var @this = (RaceTest)state!;
		Interlocked.CompareExchange(ref @this._ExceptionOrTimeout, s_TimeoutSignal, null);
		@this.DisposeTimer(); // Release resource
	};

	private void DisposeTimer() {
		var timer = _Timer;
		if (timer != null) {
			timer.Dispose(); // Thread-safe
			_Timer = null;
		}
	}

	private Task[] ConsumeCore(LinkedList<TaskItem> items) {
		var tasks = new Task[items.Count]; // Better to throw OOM early

		int timeout = _MinRunMillis;
		if (timeout <= 0) {
			_ExceptionOrTimeout = s_TimeoutSignal; // Set as already timed out
		} else {
			_Timer = new(s_TimeoutCallback, this, timeout, Timeout.Infinite);
		}

		try {
			int i = 0;
			for (var node = items.First; node != null; node = node.Next) {
				// The ff. shouldn't normally throw though
				tasks[i++] = node.Value.StartTask();
			}
			return tasks;
		} catch (Exception ex) {
			// Force termination of already running tasks
			_ExceptionOrTimeout = ex;
			throw;
		} finally {
			// Must set event, so that waiting threads get to preceed, and
			// eventually terminate.
			try {
				_StartEvent.Set(); // Also shouldn't normally throw
			} catch (Exception ex) {
#pragma warning disable CA2219 // Do not raise exceptions in finally clauses
				if (_ExceptionOrTimeout is not Exception priorEx) throw;
				throw new MinimalAggregateException(priorEx, ex);
#pragma warning restore CA2219
			}
		}
	}

	#endregion

	public Exception? Consume() {
		var items = Interlocked.Exchange(ref _TaskItems, null);
		if (items == null) return null;

		try {
			var tasks = ConsumeCore(items);
			Task.WaitAll(tasks);
			// Event can now be safely disposed
			_StartEvent.Dispose();
		} finally {
			// Dispose `Timer` set up by `ConsumeCore()`
			DisposeTimer();
		}

		return _ExceptionOrTimeout as Exception;
	}

	public async Task<Exception?> ConsumeAsync() {
		var items = Interlocked.Exchange(ref _TaskItems, null);
		if (items == null) return null;

		try {
			var tasks = ConsumeCore(items);
			await Task.WhenAll(tasks).ConfigureAwait(false);
			// Event can now be safely disposed
			_StartEvent.Dispose();
		} finally {
			// Dispose `Timer` set up by `ConsumeCore()`
			DisposeTimer();
		}

		return _ExceptionOrTimeout as Exception;
	}

	#region `IDisposable` implementation

	protected virtual void Dispose(bool disposing) {
		if (!disposing) return; // Will also skip the throwing code below

		if (Consume() is Exception ex)
			ExceptionDispatchInfo.Throw(ex);
	}

	~RaceTest() => Dispose(disposing: false);

	public void Dispose() {
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
		// ^- Side-effect: `this` is kept alive 'til the method ends.
		// - See, https://stackoverflow.com/q/816818
	}

	public async ValueTask DisposeAsync() {
		await DisposeAsyncCore().ConfigureAwait(false);

		Dispose(disposing: false);
		GC.SuppressFinalize(this);
	}

	protected virtual ValueTask DisposeAsyncCore() => new(ConsumeAsync());

	#endregion

	// --

	#region Defaults

	private const int CompetingThreadsPerProcessor = 4;
	private static readonly int DefaultNumThreads =
		Math.Max(Environment.ProcessorCount * CompetingThreadsPerProcessor, CompetingThreadsPerProcessor);

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

	#region `Task` creation

	private abstract record TaskItem {
		public abstract Task StartTask();
	}

	private const TaskCreationOptions CommonTaskCreationOptions
			= TaskCreationOptions.LongRunning
			| TaskCreationOptions.HideScheduler
			| TaskCreationOptions.DenyChildAttach;

	private sealed record TaskItemWithoutLocal(
		RaceTest Race, int RunsPerSpin,
		Action Init, Action Body, Action Finally
	) : TaskItem {

		public override Task StartTask() => Task.Factory.StartNew(
			DoTaskLoopWithoutLocal, this,
			CancellationToken.None,
			CommonTaskCreationOptions,
			TaskScheduler.Default
		);
	}

	private sealed record TaskItemWithLocal<TLocal>(
		RaceTest Race, int RunsPerSpin,
		Func<TLocal> Init, Action<TLocal> Body, Action<TLocal> Finally
	) : TaskItem {

		public override Task StartTask() => Task.Factory.StartNew(
			DoTaskLoopWithLocal<TLocal>, this,
			CancellationToken.None,
			CommonTaskCreationOptions,
			TaskScheduler.Default
		);
	}

	#endregion

	public RaceTest Queue(int numThreads, int runsPerSpin, Action @init, Action @body, Action @finally) {
		if (numThreads < 0) numThreads = DefaultNumThreads;
		if (runsPerSpin < 0) runsPerSpin = DefaultRunsPerSpin;

		var items = _TaskItems ?? throw new ObjectDisposedException(GetType().ToString());
		TaskItemWithoutLocal item = new(this, runsPerSpin, @init, @body, @finally);

		for (; numThreads > 0; numThreads--)
			items.AddLast(item);

		return this;
	}

	public RaceTest Queue<TLocal>(int numThreads, int runsPerSpin, Func<TLocal> @init, Action<TLocal> @body, Action<TLocal> @finally) {
		if (numThreads < 0) numThreads = DefaultNumThreads;
		if (runsPerSpin < 0) runsPerSpin = DefaultRunsPerSpin;

		var items = _TaskItems ?? throw new ObjectDisposedException(GetType().ToString());
		TaskItemWithLocal<TLocal> item = new(this, runsPerSpin, @init, @body, @finally);

		for (; numThreads > 0; numThreads--)
			items.AddLast(item);

		return this;
	}

	private static void DoTaskLoopWithoutLocal(object? state) {
		var (race, runsPerSpin, @init, @body, @finally) = (TaskItemWithoutLocal)state!;

		int spins = race._MinSpinsPerThread;
		Exception? exitEx = null;

		try {
			@init();
		} catch (Exception ex) {
			exitEx = ex;
			goto ExitWithException;
		}

		try {
			race._StartEvent.Wait(); // Should throw only on extremely rare case

			for (; spins > 0; spins--) {
				if (Volatile.Read(ref race._ExceptionOrTimeout) is Exception) {
					goto Cleanup;
				}
				for (int i = runsPerSpin; i > 0; i--) {
					@body();
				}
			}
			while (Volatile.Read(ref race._ExceptionOrTimeout) == null) {
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
			if (exitEx != null) {
				exitEx = new AggregateException(exitEx, ex);
				goto ExitWithException;
			}
		}

		if (exitEx == null) {
			return;
		}

	ExitWithException:
		if (Interlocked.CompareExchange(ref race._ExceptionOrTimeout, exitEx, null) != null) {
			Interlocked.CompareExchange(ref race._ExceptionOrTimeout, exitEx, s_TimeoutSignal);
		}
	}

	private static void DoTaskLoopWithLocal<TLocal>(object? state) {
		var (race, runsPerSpin, @init, @body, @finally) = (TaskItemWithLocal<TLocal>)state!;

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
			race._StartEvent.Wait(); // Should throw only on extremely rare case

			for (; spins > 0; spins--) {
				if (Volatile.Read(ref race._ExceptionOrTimeout) is Exception) {
					goto Cleanup;
				}
				for (int i = runsPerSpin; i > 0; i--) {
					@body(local);
				}
			}
			while (Volatile.Read(ref race._ExceptionOrTimeout) == null) {
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
			if (exitEx != null) {
				exitEx = new AggregateException(exitEx, ex);
				goto ExitWithException;
			}
		}

		if (exitEx == null) {
			return;
		}

	ExitWithException:
		if (Interlocked.CompareExchange(ref race._ExceptionOrTimeout, exitEx, null) != null) {
			Interlocked.CompareExchange(ref race._ExceptionOrTimeout, exitEx, s_TimeoutSignal);
		}
	}

	#endregion
}
