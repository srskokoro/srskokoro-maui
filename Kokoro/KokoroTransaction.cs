namespace Kokoro;

public class KokoroTransaction : IDisposable, IAsyncDisposable {

	internal readonly uint _key;
	private KokoroContext? _context;

	public KokoroContext Context => _context ?? throw new ObjectDisposedException(nameof(KokoroTransaction));

	protected internal KokoroTransaction(KokoroContext context) {
		try {
			_context = context;
			_key = context.OnInitTransaction();
		} catch {
#pragma warning disable CA1816 // Dispose methods should call SuppressFinalize
			GC.SuppressFinalize(this);
#pragma warning restore CA1816
			throw;
		}
	}

	public virtual void Commit() {
		Context.OnCommitTransaction(this);
		_context = null;
	}

	public virtual void Rollback() {
		Context.OnRollbackTransaction(this);
		_context = null;
	}

	// --

	public bool IsDisposed => _context == null;

	// https://docs.microsoft.com/en-us/dotnet/standard/garbage-collection/implementing-dispose
	protected virtual void Dispose(bool disposing) {
		var context = _context;
		if (context is null) return;

		context.OnDisposeTransaction(this, disposing);
		_context = null;
	}

	// Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
	~KokoroTransaction() => Dispose(false);

	public void Dispose() {
		// Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
		Dispose(true);
		GC.SuppressFinalize(this);
	}

#pragma warning disable CA1816 // Dispose methods should call SuppressFinalize
	public virtual ValueTask DisposeAsync() {
		Dispose();
		return default;
	}
#pragma warning restore CA1816
}
