namespace Kokoro.Internal;
using Kokoro.Internal.Sqlite;

internal sealed class DataToken : IDisposable {
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
	internal DataToken? _Next;

	public KokoroSqliteDb Db => _Db ?? throw Ex_ODisposed();
	public KokoroContext Context => _Context ?? throw Ex_ODisposed();
	public KokoroCollection Collection => _Collection ?? throw Ex_ODisposed();
	public DataToken Next => _Next ?? throw Ex_ODisposed();

	public DataToken(KokoroCollection collection) {
		_Db = collection.Db;
		_Context = collection.Context;
		_Collection = collection;
		_Next = this;
	}

	public DataToken(KokoroSqliteDb db, KokoroContext context, KokoroCollection collection) {
		_Db = db;
		_Context = context;
		_Collection = collection;
		_Next = this;
	}

	public void DisplacedBy(DataToken? next) {
		DataMark = DataMarkDisposed;
		_Db = null;
		_Context = null;
		_Collection = null;
		_Next = next;
	}

	public void Dispose() => DisplacedBy(null);

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static ObjectDisposedException Ex_ODisposed() => DisposeUtils.Ode(typeof(DataToken));
}
