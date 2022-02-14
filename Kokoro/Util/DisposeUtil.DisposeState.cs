namespace Kokoro.Util;

static partial class DisposeUtil {

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static DisposeState VolatileRead(ref this DisposeState currentDisposeState) {
		return (DisposeState)Volatile.Read(ref Unsafe.As<DisposeState, uint>(ref currentDisposeState));
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static void VolatileWrite(ref this DisposeState currentDisposeState, DisposeState newDisposeState) {
		Volatile.Write(ref Unsafe.As<DisposeState, uint>(ref currentDisposeState), (uint)newDisposeState);
	}


	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static bool IsNotDisposed(ref this DisposeState currentDisposeState) {
		return currentDisposeState.VolatileRead() == DisposeState.None;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static bool IsDisposed(ref this DisposeState currentDisposeState) {
		return currentDisposeState.VolatileRead() != DisposeState.None;
	}


	public static bool HandleDisposeRequest(ref this DisposeState currentDisposeState) {
		DisposeState oldDisposeState = currentDisposeState;
		return oldDisposeState < DisposeState.Disposing_Flag && Interlocked.CompareExchange(
			ref Unsafe.As<DisposeState, uint>(ref currentDisposeState)
			, (uint)DisposeState.Disposing
			, (uint)oldDisposeState
		) == (uint)oldDisposeState;
	}

	public static void CommitDisposeRequest(ref this DisposeState currentDisposeState) {
		Assert_CurrentlyDisposing(ref currentDisposeState);
		currentDisposeState.VolatileWrite(DisposeState.DisposedFully);
	}

	public static void RevertDisposeRequest(ref this DisposeState currentDisposeState) {
		Assert_CurrentlyDisposing(ref currentDisposeState);
		currentDisposeState.VolatileWrite(DisposeState.DisposedPartially);
	}


	[Conditional("DEBUG")]
	private static void Assert_CurrentlyDisposing(ref DisposeState currentDisposeState) {
		Debug.Assert(currentDisposeState.VolatileRead() == DisposeState.Disposing);
	}
}
