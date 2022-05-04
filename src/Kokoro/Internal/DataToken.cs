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

	internal KokoroSqliteDb Db => _Db ?? Latest._Db ?? throw Ex_ODisposed();
	internal KokoroContext Context => _Context ?? Latest._Context ?? throw Ex_ODisposed();
	internal KokoroCollection Collection => _Collection ?? Latest._Collection ?? throw Ex_ODisposed();

	internal DataToken Latest {
		// Don't inline, as this is used only for rare cases
		[MethodImpl(MethodImplOptions.NoInlining)]
		get {
			DataToken cur = this;
			while (cur._Next != null) {
				cur = cur._Next;
			}
			return cur;
		}
	}

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
