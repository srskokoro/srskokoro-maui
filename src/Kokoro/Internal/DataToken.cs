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

	private DataToken? _Next;

	internal KokoroSqliteDb Db => _Db ?? ResolveDb();
	internal KokoroContext Context => _Context ?? ResolveContext();
	internal KokoroCollection Collection => _Collection ?? ResolveCollection();

	#region Fallback Mechanisms

	// Not inlined, as these are meant to be used much rarely
	[MethodImpl(MethodImplOptions.NoInlining)] internal KokoroSqliteDb ResolveDb() => Latest._Db ?? throw Ex_ODisposed();
	[MethodImpl(MethodImplOptions.NoInlining)] internal KokoroContext ResolveContext() => Latest._Context ?? throw Ex_ODisposed();
	[MethodImpl(MethodImplOptions.NoInlining)] internal KokoroCollection ResolveCollection() => Latest._Collection ?? throw Ex_ODisposed();

	internal DataToken Latest {
		get {
			DataToken cur = this;
			while (cur._Next != null) {
				cur = cur._Next;
			}
			return cur;
		}
	}

	#endregion

	public DataToken(KokoroCollection collection)
		: this(collection.Db, collection.Context, collection) { }

	internal DataToken(KokoroSqliteDb db, KokoroContext context, KokoroCollection collection) {
		_Db = db;
		_Context = context;
		_Collection = collection;
	}

	internal void DisplacedBy(DataToken? next) {
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
