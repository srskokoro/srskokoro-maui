namespace Kokoro.Common.Util;

internal static class DisposeStates {

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
		// Ternary operator returning true/false prevents redundant asm generation:
		// See, https://github.com/dotnet/runtime/issues/4207#issuecomment-147184273
		return currentDisposeState.VolatileRead() == DisposeState.None ? true : false;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static bool IsNotDisposed_NV(this DisposeState currentDisposeState) {
		// Ternary operator returning true/false prevents redundant asm generation:
		// See, https://github.com/dotnet/runtime/issues/4207#issuecomment-147184273
		return currentDisposeState == DisposeState.None ? true : false;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static bool IsDisposed(ref this DisposeState currentDisposeState) {
		// Ternary operator returning true/false prevents redundant asm generation:
		// See, https://github.com/dotnet/runtime/issues/4207#issuecomment-147184273
		return currentDisposeState.VolatileRead() != DisposeState.None ? true : false;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static bool IsDisposed_NV(this DisposeState currentDisposeState) {
		// Ternary operator returning true/false prevents redundant asm generation:
		// See, https://github.com/dotnet/runtime/issues/4207#issuecomment-147184273
		return currentDisposeState != DisposeState.None ? true : false;
	}


	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static bool CanHandleDisposeRequest(ref this DisposeState currentDisposeState) {
		// Ternary operator returning true/false prevents redundant asm generation:
		// See, https://github.com/dotnet/runtime/issues/4207#issuecomment-147184273
		return currentDisposeState.VolatileRead() < DisposeState.Disposing_Flag ? true : false;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static bool CanHandleDisposeRequest_NV(this DisposeState currentDisposeState) {
		// Ternary operator returning true/false prevents redundant asm generation:
		// See, https://github.com/dotnet/runtime/issues/4207#issuecomment-147184273
		return currentDisposeState < DisposeState.Disposing_Flag ? true : false;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static bool CannotHandleDisposeRequest(ref this DisposeState currentDisposeState) {
		// Ternary operator returning true/false prevents redundant asm generation:
		// See, https://github.com/dotnet/runtime/issues/4207#issuecomment-147184273
		return currentDisposeState.VolatileRead() >= DisposeState.Disposing_Flag ? true : false;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static bool CannotHandleDisposeRequest_NV(this DisposeState currentDisposeState) {
		// Ternary operator returning true/false prevents redundant asm generation:
		// See, https://github.com/dotnet/runtime/issues/4207#issuecomment-147184273
		return currentDisposeState >= DisposeState.Disposing_Flag ? true : false;
	}


	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static bool HandleDisposeRequest(ref this DisposeState currentDisposeState) {
		DisposeState oldDisposeState = currentDisposeState;
		// Ternary operator returning true/false prevents redundant asm generation:
		// See, https://github.com/dotnet/runtime/issues/4207#issuecomment-147184273
		return oldDisposeState.CanHandleDisposeRequest_NV() && Interlocked.CompareExchange(
			ref Unsafe.As<DisposeState, uint>(ref currentDisposeState)
			, (uint)DisposeState.Disposing
			, (uint)oldDisposeState
		) == (uint)oldDisposeState ? true : false;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static void CommitDisposeRequest(ref this DisposeState currentDisposeState) {
		// A volatile read is not needed here. We're simply verifying wether or
		// not the method calling us is being used correctly.
		if (currentDisposeState != DisposeState.Disposing) {
			CommitDisposeRequest__E_Misuse_InvOp();
		}
		currentDisposeState.VolatileWrite(DisposeState.DisposedFully);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static void RevokeDisposeRequest(ref this DisposeState currentDisposeState) {
		// A volatile read is not needed here. We're simply verifying wether or
		// not the method calling us is being used correctly.
		if (currentDisposeState != DisposeState.Disposing) {
			return; // NOP -- let `CommitDisposeRequest()` throw for us instead
		}
		currentDisposeState.VolatileWrite(DisposeState.DisposedPartially);
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[DoesNotReturn]
	private static void CommitDisposeRequest__E_Misuse_InvOp()
		=> throw new InvalidOperationException($"Operation is valid only if currently disposing or handling a dispose request.");
}
