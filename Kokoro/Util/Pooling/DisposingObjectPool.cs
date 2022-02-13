namespace Kokoro.Util.Pooling;

internal class DisposingObjectPool<T> : ObjectPool<T>, IDisposable where T : IDisposable {
	private int _DisposeState;

	private enum DisposeState {
		NotDisposed = 0,
		DisposeRequested = 1,
		DisposedFully = 2,
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public override bool TryPool(T poolable) {
		if (base.TryPool(poolable)) {
			// If already disposed or disposing, we retake the added object, or
			// take any object that can be taken. That way, we can dispose the
			// added object instead. Note: It is important that we only dispose
			// objects that are no longer in the pool, which is why we must
			// retake the added object. It also important that we retake the
			// added object since once we are already fully disposed, no one
			// else will dispose the newly added object.
			if (Volatile.Read(ref _DisposeState) == (int)DisposeState.NotDisposed || !Retake_NoInterrupts(out poolable!)) {
				// Return successfully as well if we failed to retake the added
				// object, or any object: it simply means someone else already
				// took care of it. The one who took the object may however
				// choose not to dispose it, i.e., `TryTakeAggressively()`.
				return true;
			}
			// If an object, any object, was taken out while we're already
			// disposed, we must dispose it as well, or no one else will.
		}
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

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public override bool TryTake([MaybeNullWhen(false)] out T poolable) {
		if (base.TryTake(out var taken)) {
			if (Volatile.Read(ref _DisposeState) == (int)DisposeState.NotDisposed) {
				poolable = taken;
				return true;
			}
		}
		poolable = default;
		return false;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public virtual bool TryTakeAggressively([MaybeNullWhen(false)] out T poolable) {
		if (Volatile.Read(ref _DisposeState) != (int)DisposeState.NotDisposed) {
			poolable = default;
			return false;
		}
		// A request to dispose may have already been issued at this point, but
		// so long as we can take something out from the pool, we won't care.
		// The fact that we can take something out from the pool either means
		// that we're still not yet fully disposed or that there is at least
		// one other thread that keeps on adding more to the pool even when the
		// dispose operation has already commenced.
		return base.TryTake(out poolable);
	}

	protected virtual void Dispose(bool disposing) {
		if (!disposing) return;

		if (Interlocked.CompareExchange(ref _DisposeState, (int)DisposeState.DisposeRequested
			, (int)DisposeState.NotDisposed) != (int)DisposeState.NotDisposed) {
			// Someone is already disposing or has already disposed us.
			return;
		}
		// Successfully acquired an "exclusive" access to perform disposal.
		try {
			// All objects that are in the pool belongs to the pool. So we must
			// first take them out of the pool before we can do whatever we
			// want with them (i.e., before we can dispose them).
			while (base.TryTake(out var poolable)) {
				// This poolable now only belongs to us, therefore we can proceed.
				poolable.Dispose();
			}
		} catch {
			// Failed to dispose everything. Let the next caller of this method
			// continue the disposing operation instead.
			Debug.Assert(Volatile.Read(ref _DisposeState) == (int)DisposeState.DisposeRequested);
			// Relinquish access
			Volatile.Write(ref _DisposeState, (int)DisposeState.NotDisposed);
			throw;
		}
		// A volatile write should really suffice at this point, but just in
		// case we're wrong, here's an assertion proving that we're the only
		// ones who got here due to our exclusive access.
		Debug.Assert(Volatile.Read(ref _DisposeState) == (int)DisposeState.DisposeRequested);
		// Done
		Volatile.Write(ref _DisposeState, (int)DisposeState.DisposedFully);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void Dispose() {
		Dispose(true);
		GC.SuppressFinalize(this);
	}
}
