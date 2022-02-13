namespace Kokoro.Util;

internal enum DisposeState : int {
	NotDisposed = 0,
	DisposeRequested = 1,
	DisposedFully = 2|DisposeRequested,
}
