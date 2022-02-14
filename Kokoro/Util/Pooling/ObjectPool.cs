using System.Collections.Concurrent;

namespace Kokoro.Util.Pooling;

/// <summary>
/// A lighter alternative to, <see href="https://www.nuget.org/packages/Microsoft.Extensions.ObjectPool"/>
/// </summary>
internal class ObjectPool<T> {
	private protected static readonly int _DefaultMaxSize = Environment.ProcessorCount * 2;

	private protected readonly ConcurrentStack<T> _Pool = new();
	// Rules to assure correct behavior:
	// - Increment "before" adding to pool.
	// - Decrement only "after" taking from pool.
	private protected int _Size;

	// --

	public virtual int MaxSize => _DefaultMaxSize;

	public int Size => _Size;

	/// <exception cref="ThreadInterruptedException"></exception>
	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	public virtual bool TryTake([MaybeNullWhen(false)] out T poolable) {
		// May throw `ThreadInterruptedException`
		if (_Pool.TryPop(out poolable)) {
			Interlocked.Decrement(ref _Size);

			// Shouldn't happen since we decrement only after taking from the
			// pool, when we increment only before adding to the pool.
			Debug.Assert(_Size >= 0);
			return true;
		}
		return false;
	}

	/// <exception cref="ThreadInterruptedException"></exception>
	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	public virtual bool TryPool(T poolable) {
		int maxSize = MaxSize;
		int oldSize = Volatile.Read(ref _Size);
		if (oldSize >= maxSize) {
			return false;
		}
		if (Interlocked.CompareExchange(ref _Size, oldSize + 1, oldSize) != oldSize) {
			// If we failed, go to the slow path
			SpinWait spin = default;
			// Keep trying to CAS until we succeed
			do {
				// May throw `ThreadInterruptedException`
				spin.SpinOnce(sleep1Threshold: -1);

				// Re-read and check
				oldSize = _Size; // A volatile read is unnecessary at this point
				if (oldSize >= maxSize) {
					return false;
				}
			} while (Interlocked.CompareExchange(ref _Size, oldSize + 1, oldSize) != oldSize);
		}

		try {
			// May throw `ThreadInterruptedException`
			_Pool.Push(poolable);
		} catch (OutOfMemoryException) {
			// Our internal stack can no longer expand.
			// Simply swallow the OOM exception: To the caller's perspective,
			// returning an object shouldn't cause an allocation, since it's
			// simply returning an already allocated resource back to the pool.
			return false;
		}
		return true;
	}
}
