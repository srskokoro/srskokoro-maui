namespace Kokoro.Util;

internal enum DisposeState : int {
	NotDisposed = 0,
	DisposeRequested = 1,
	DisposedFully = 2|DisposeRequested,

#pragma warning disable CA1069 // Enums values should not be duplicated
	#region Aliases

	DisposedPartially = DisposeRequested,
	Disposed = DisposedPartially,
	DisposeCommitted = DisposedFully,

	#endregion
#pragma warning restore CA1069 // Enums values should not be duplicated
}
