namespace Kokoro.Internal;
using Kokoro.Internal.Sqlite;

internal class DataToken : IDisposable {
	internal const nuint DataMarkDisposed = 0;
	internal const nuint DataMarkInit = 1;

	/// <summary>
	/// Represents a point in time when changes was last observed. If the value
	/// changes, then it is an indication that the collection data has been
	/// modified.
	/// </summary>
	internal nuint DataMark = DataMarkInit;

	internal KokoroSqliteDb? _Db;
	internal KokoroContext? _Context;
	internal KokoroCollection? _Collection;

	internal KokoroSqliteDb Db => _Db ?? throw Ex_ODisposed();
	internal KokoroContext Context => _Context ?? throw Ex_ODisposed();
	internal KokoroCollection Collection => _Collection ?? throw Ex_ODisposed();

	public DataToken(KokoroCollection collection) {
		_Db = collection.Db;
		_Context = collection.Context;
		_Collection = collection;
	}

	public DataToken(KokoroSqliteDb db, KokoroContext context, KokoroCollection collection) {
		_Db = db;
		_Context = context;
		_Collection = collection;
	}

	public void Dispose() {
		DataMark = DataMarkDisposed;
		_Db = null;
		_Context = null;
		_Collection = null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private ObjectDisposedException Ex_ODisposed() => DisposeUtils.Ode(GetType());
}
