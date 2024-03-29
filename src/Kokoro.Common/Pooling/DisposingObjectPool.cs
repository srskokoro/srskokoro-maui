﻿namespace Kokoro.Common.Pooling;

internal class DisposingObjectPool<T> : ObjectPool<T>, IDisposable where T : IDisposable {
	private DisposeStatePlain _DisposeState;

	protected ref DisposeStatePlain DisposeState => ref _DisposeState;

	public bool IsDisposed => _DisposeState.IsDisposed();

	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	public override bool TryPool(T poolable) {
		int maxSize = MaxSize;
		int oldSize = Volatile.Read(ref _Size);
		if (oldSize >= maxSize) {
			goto Reject;
		}
		if (Interlocked.CompareExchange(ref _Size, oldSize + 1, oldSize) != oldSize) {
			try {
				// If we failed, go to the slow path
				SpinWait spin = default;
				// Keep trying to CAS until we succeed
				do {
					// May throw `ThreadInterruptedException`
					spin.SpinOnce(sleep1Threshold: -1);

					// Re-read and check
					oldSize = _Size; // A volatile read is unnecessary at this point
					if (oldSize >= maxSize) {
						goto Reject;
					}
				} while (Interlocked.CompareExchange(ref _Size, oldSize + 1, oldSize) != oldSize);
			} catch (ThreadInterruptedException) {
				// Prevent leakage of resource that might never get disposed
				poolable.DisposeSafely();
				throw;
			}
		}

		try {
			// May throw `ThreadInterruptedException`
			_Pool.Push(poolable);
		} catch (OutOfMemoryException) {
			// Our internal stack can no longer expand.
			// Simply swallow the OOM exception: To the caller's perspective,
			// returning an object shouldn't cause an allocation, since it's
			// simply returning an already allocated resource back to the pool.
			goto Reject;
		} catch (ThreadInterruptedException) {
			// Prevent leakage of resource that might never get disposed
			poolable.DisposeSafely();
			throw;
		}

		// If already disposed or disposing, we retake the added object, or
		// take any object that can be taken. That way, we can dispose the
		// added object instead. Note: It is important that we only dispose
		// objects that are no longer in the pool, which is why we must retake
		// the added object. It is also important that we retake the added
		// object since once we are already fully disposed, no one else will
		// dispose the newly added object.
		if (_DisposeState.IsNotDisposed() || !Retake_NoInterrupts(out poolable!)) {
			// Return successfully as well if we failed to retake the added
			// object, or any object: it simply means someone else already took
			// care of it. The one who took the object may however choose not
			// to dispose it, i.e., `TryTakeAggressively()`.
			return true;
		}
		// If an object, any object, was taken out while we're already disposed,
		// we must dispose it as well, or no one else will.
		;
	Reject:
		poolable.Dispose();
		return false;
	}

	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	private bool Retake_NoInterrupts([MaybeNullWhen(false)] out T poolable) {
		try {
			return base.TryTake(out poolable);
		} catch (ThreadInterruptedException) {
			try {
				for (; ; ) {
					try {
						return base.TryTake(out poolable);
					} catch (ThreadInterruptedException) {
						// Ignore
					}
				}
			} finally {
				// Restore "interrupted" state
				Thread.CurrentThread.Interrupt();
			}
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public override bool TryTake([MaybeNullWhen(false)] out T poolable) {
		if (base.TryTake(out var taken)) {
			if (_DisposeState.IsNotDisposed()) {
				poolable = taken;
				return true;
			}
			// If an object was taken out while we're already disposed, we must
			// dispose it as well, or no one else will.
			taken.Dispose();
		}
		poolable = default;
		return false;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public virtual bool TryTakeAggressively([MaybeNullWhen(false)] out T poolable) {
		if (_DisposeState.IsDisposed()) {
			poolable = default;
			return false;
		}
		// A request to dispose may have already been issued at this point, but
		// so long as we can take something out from the pool, we won't care.
		// The fact that we can take something out from the pool either means
		// that we're still not yet fully disposed or that there is at least
		// one other thread that keeps on adding more to the pool even when a
		// dispose operation has already kicked in.
		return base.TryTake(out poolable);
	}

	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	protected virtual void Dispose(bool disposing) {
		if (!disposing) return;

		if (!_DisposeState.HandleDisposeRequest()) {
			// Someone is already disposing or has already disposed us.
			return;
		}
		// Successfully acquired an "exclusive" access to perform disposal.

		bool interrupted = false;
		ICollection<Exception>? exc = null;

		// All objects that are in the pool belongs to the pool. So we must
		// first take them out of the pool before we can do whatever we want
		// with them (i.e., before we can dispose them).
		for (; ; ) {
			try {
				if (!base.TryTake(out var poolable)) break;
				// This poolable now only belongs to us, thus we can proceed.
				poolable.Dispose();
			} catch (Exception ex) {
				// This block is meant to catch `ThreadInterruptedException` and
				// `Dispose()` shouldn't normally throw. However, just in case
				// `Dispose()` did throw, we collect the exception, then let the
				// GC handle finalization of the disposable, given that we had
				// freed it from the pool already.
				ExceptionUtils.Collect(ref exc, ex);
				if (ex is ThreadInterruptedException) {
					interrupted = true;
				}
			}
		}

		// Re-throw any pending exception, especially `ThreadInterruptedException`
		if (exc != null) {
			Exception? ex = exc.Consolidate();
			// Let callers catch `ThreadInterruptedException` in a
			// straightforward manner.
			if (interrupted && ex is not ThreadInterruptedException) {
				ex = new ThreadInterruptedException(null, ex);
			}
			ex?.ReThrow();
		}
	}

	public void Dispose() {
		Dispose(true);
		GC.SuppressFinalize(this);
	}
}
