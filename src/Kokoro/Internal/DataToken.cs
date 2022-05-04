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

	internal KokoroSqliteDb? Db;
	internal KokoroContext? Context;
	internal KokoroCollection? Collection;
	internal DataToken? Next;

	public DataToken(KokoroCollection collection)
		: this(collection.Db, collection.Context, collection) { }

	internal DataToken(KokoroSqliteDb db, KokoroContext context, KokoroCollection collection) {
		Db = db;
		Context = context;
		Collection = collection;
		Next = this;
	}

	internal void DisplacedBy(DataToken? next) {
		DataMark = DataMarkDisposed;
		Db = null;
		Context = null;
		Collection = null;
		Next = next;
	}

	public void Dispose() => DisplacedBy(null);

	[MethodImpl(MethodImplOptions.NoInlining)]
	internal static ObjectDisposedException Ex_ODisposed() => DisposeUtils.Ode(typeof(DataToken));
}
