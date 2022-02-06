namespace Kokoro;

/// <remarks>Not thread-safe.</remarks>
public class KokoroTransaction : IDisposable, IAsyncDisposable {

	internal readonly uint key;
	private KokoroContext? context;

	public KokoroContext Context => context ?? throw MakeTransactionCompletedException();
	public KokoroContext? GetContextOrNull() => context;

	protected internal KokoroTransaction(KokoroContext context) {
		try {
			this.context = context;
			this.key = context.OnInitTransaction();
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
		context = null;
	}

	public virtual void Rollback() {
		Context.OnRollbackTransaction(this);
		context = null;
	}

	// https://docs.microsoft.com/en-us/dotnet/standard/garbage-collection/implementing-dispose
	protected virtual void Dispose(bool disposing) {
		var context = this.context;
		if (context is null) return;

		context.OnDisposeTransaction(this, disposing);
		this.context = null;
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
