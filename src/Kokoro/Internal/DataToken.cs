namespace Kokoro.Internal;
using Kokoro.Internal.Sqlite;

internal class DataToken {
	internal const nuint DataMarkExhausted = 0;
	internal const nuint DataMarkInit = 1;

	/// <summary>
	/// Represents a point in time when changes was last observed. If the value
	/// changes, then it is an indication that the collection data has been
	/// modified.
	/// </summary>
	internal nuint DataMark = DataMarkInit;

	internal readonly KokoroSqliteDb Db;
	internal readonly KokoroContext Context;
	internal readonly KokoroCollection Collection;

	public DataToken(KokoroCollection collection) {
		Db = collection.Db;
		Context = collection.Context;
		Collection = collection;
	}

	public DataToken(KokoroSqliteDb db, KokoroContext context, KokoroCollection collection) {
		Db = db;
		Context = context;
		Collection = collection;
	}
}
