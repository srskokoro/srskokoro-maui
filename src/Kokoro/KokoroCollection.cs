namespace Kokoro;
using Kokoro.Internal.Sqlite;

/// <remarks>Not thread-safe.</remarks>
public class KokoroCollection : IDisposable {
	internal KokoroContext _Context;
	internal KokoroSqliteDb _Db;

	public KokoroCollection(KokoroContext context) {
		MarkUsage(context); // May throw on failure
		try {
			// Throws on incompatible schema version
			_Db = context.ObtainOperableDb();

			// Success!
			_Context = context;
		} catch {
			// Failed!
			UnMarkUsage(context);

#pragma warning disable CA1816 // Dispose methods should call SuppressFinalize
			GC.SuppressFinalize(this);
#pragma warning restore CA1816

			throw;
		}
	}

	private protected virtual void MarkUsage(KokoroContext context) => context.MarkUsageShared();
	private protected virtual void UnMarkUsage(KokoroContext context) => context.UnMarkUsageShared();

	#region `IDisposable` implementation

	protected virtual void Dispose(bool disposing) {
		var context = _Context;
		if (context == null) {
			// Already disposed
			return;
		}

		if (disposing) {
			// Dispose managed state (managed objects).
			//
			// NOTE: If we're here, then we're sure that the constructor
			// completed successfully. Fields that aren't supposed to be
			// null are guaranteed to be non-null, unless we set fields to
			// null only to be called again due to a previous failed
			// dispose attempt.
			// --
		}

		// Here we should free unmanaged resources (unmanaged objects),
		// override finalizer, and set large fields to null.
		//
		// NOTE: Make sure to check for null fields, for when the
		// constructor fails to complete or even execute, and the finalizer
		// calls us anyway. See also, https://stackoverflow.com/q/34447080
		// --

		var db = _Db;
		if (db != null) {
			_Db = null!;
			// ^ Prevents recycling when already recycled before. Also, we did
			// that first in case the recycle op succeeds and yet it throws
			// before it can return to us.
			context.RecycleOperableDb(db);
		}

		UnMarkUsage(context);
		_Context = null!; // Marks disposal as successful
	}

	~KokoroCollection() => Dispose(disposing: false);

	public void Dispose() {
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
		// ^- Side-effect: `this` is kept alive 'til the method ends.
		// - See, https://stackoverflow.com/q/816818
	}

	#endregion
}
