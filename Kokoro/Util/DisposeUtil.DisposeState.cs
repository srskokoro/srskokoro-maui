namespace Kokoro.Util;

static partial class DisposeUtil {

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static bool IsNotDisposed(ref this DisposeState currentDisposeState) {
		return Volatile.Read(ref Unsafe.As<DisposeState, int>(ref currentDisposeState)) == (int)DisposeState.NotDisposed;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static bool IsDisposed(ref this DisposeState currentDisposeState) {
		return Volatile.Read(ref Unsafe.As<DisposeState, int>(ref currentDisposeState)) != (int)DisposeState.NotDisposed;
	}


	public static bool HandleDisposeRequest(ref this DisposeState currentDisposeState) {
		return Interlocked.CompareExchange(
			ref Unsafe.As<DisposeState, int>(ref currentDisposeState)
			, (int)DisposeState.DisposeRequested
			, (int)DisposeState.NotDisposed
		) == (int)DisposeState.NotDisposed;
	}

	public static void CommitDisposeRequest(ref this DisposeState currentDisposeState) {
		AssertDisposeRequested(ref currentDisposeState);
		Volatile.Write(ref Unsafe.As<DisposeState, int>(ref currentDisposeState), (int)DisposeState.DisposedFully);
	}

	public static void RevertDisposeRequest(ref this DisposeState currentDisposeState) {
		AssertDisposeRequested(ref currentDisposeState);
		Volatile.Write(ref Unsafe.As<DisposeState, int>(ref currentDisposeState), (int)DisposeState.NotDisposed);
	}


	[Conditional("DEBUG")]
	private static void AssertDisposeRequested(ref DisposeState currentDisposeState) {
		Debug.Assert(Volatile.Read(ref Unsafe.As<DisposeState, int>(ref currentDisposeState)) == (int)DisposeState.DisposeRequested);
	}
}
