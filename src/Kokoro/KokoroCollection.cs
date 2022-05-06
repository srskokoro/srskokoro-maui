namespace Kokoro;
using Kokoro.Internal;
using Kokoro.Internal.Sqlite;

/// <remarks>Not thread-safe.</remarks>
public class KokoroCollection : IDisposable {
	private KokoroContext? _Context;
	private KokoroSqliteDb? _Db;

	#region Primary Properties

	public KokoroContext Context {
		get {
			var _ = _Context;
			if (_ != null)
				return _;
			E_ODisposed();
			throw null;
		}
	}

	internal KokoroSqliteDb Db {
		get {
			var _ = _Db;
			if (_ != null)
				return _;
			E_ODisposed();
			throw null;
		}
	}

	internal DataToken DataToken {
		get {
			var token = Db.DataToken!;
			Debug.Assert(token != null);
			return token;
		}
	}

	#region Nullable Access

	public KokoroContext? ContextOrNull => _Context;
	internal KokoroSqliteDb? DbOrNull => _Db;

	internal DataToken? DataTokenOrNull => _Db?.DataToken;

	#endregion

	#endregion

	public KokoroCollection(KokoroContext context) {
		MarkUsage(context); // May throw on failure
		try {
			// Throws on incompatible schema version
			var db = context.ObtainOperableDb();
			db.SetUpWith(context, this);
			_Db = db;
			_Context = context; // Success!
		} catch (Exception ex) {
#pragma warning disable CA1816 // Dispose methods should call SuppressFinalize
			GC.SuppressFinalize(this);
#pragma warning restore CA1816
			try {
				UnMarkUsage(context, disposing: true);
			} catch (Exception ex2) {
				throw new DisposeAggregateException(ex, ex2);
			}
			throw;
		}
	}

	private protected virtual void MarkUsage(KokoroContext context) => context.MarkUsageShared();
	private protected virtual void UnMarkUsage(KokoroContext context, bool disposing) => context.UnMarkUsageShared(disposing);

	#region `IDisposable` implementation

	// NOTE: We can also be partially disposed, such as when `_Db` is now null,
	// but having a null `_Context` means that we have been completely disposed.
	public bool IsDisposedFully => _Context == null;

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
			// completed successfully. Fields that aren't supposed to be null
			// are guaranteed to be non-null, unless we exposed `this` before
			// construction could end (then called `Dispose()` on it after), or
			// we set fields to null only to be called again due to a previous
			// failed dispose attempt.
			// --

			var db = _Db;
			if (db != null) {
				db.TearDown();
				_Db = null;
				// ^ Prevents recycling when already recycled before. Also, we
				// did that first in case the recycle op succeeds and yet it
				// throws before it can return to us.
				context.RecycleOperableDb(db);
			}
		}

		// Here we should free unmanaged resources (unmanaged objects), override
		// finalizer, and set large fields to null.
		//
		// NOTE: Make sure to check for null fields, for when the constructor
		// fails to complete or even execute, and the finalizer calls us anyway.
		// See also, https://stackoverflow.com/q/34447080
		// --

		_Context = null; // Marks disposal as successful
		UnMarkUsage(context, disposing);
		// ^ Done last as it's allowed to throw when disposing.
		// ^ Note however that, it shouldn't throw when simply unmarking usage.
	}

	~KokoroCollection() => Dispose(disposing: false);

	public void Dispose() {
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
		// ^- Side-effect: `this` is kept alive 'til the method ends.
		// - See, https://stackoverflow.com/q/816818
	}

	[DoesNotReturn]
	private void E_ODisposed() => throw Ex_ODisposed();

	[MethodImpl(MethodImplOptions.NoInlining)]
	private ObjectDisposedException Ex_ODisposed() => DisposeUtils.Ode(GetType());

	#endregion
}
