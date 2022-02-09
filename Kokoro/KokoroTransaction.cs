namespace Kokoro;

/// <remarks>Not thread-safe.</remarks>
public class KokoroTransaction : IDisposable, IAsyncDisposable {

	internal readonly uint _Key;
	private KokoroContext? _Context;

	public KokoroContext Context => _Context ?? throw MakeTransactionCompletedException();
	public KokoroContext? GetContextOrNull() => _Context;

	protected internal KokoroTransaction(KokoroContext context) {
		try {
			_Context = context;
			_Key = context.OnInitTransaction();
		} catch {
#pragma warning disable CA1816 // Dispose methods should call SuppressFinalize
			GC.SuppressFinalize(this);
#pragma warning restore CA1816
			throw;
		}
	}

	internal static Exception MakeTransactionCompletedException() {
		return new InvalidOperationException($"`{nameof(KokoroTransaction)}` has completed already; it's no longer usable.");
	}

	public virtual void Commit() {
		Context.OnCommitTransaction(this);
		_Context = null;
	}

	public virtual void Rollback() {
		Context.OnRollbackTransaction(this);
		_Context = null;
	}

	// https://docs.microsoft.com/en-us/dotnet/standard/garbage-collection/implementing-dispose
	protected virtual void Dispose(bool disposing) {
		var context = _Context;
		if (context is null) return;

		context.OnDisposeTransaction(this, disposing);
		_Context = null;
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
