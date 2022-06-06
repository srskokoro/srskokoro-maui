namespace Kokoro.Internal;
using Kokoro.Internal.Sqlite;

/// <remarks>
/// This object is always directly tied to a <see cref="KokoroCollection"/>
/// object, with disposal of the latter object meant to cause disposal of this
/// object as well.
/// </remarks>
internal class InvalidationSource : IDisposable {
	internal const nuint DataMarkExhausted = 0;
	internal const nuint DataMarkInit = 1;

	/// <summary>
	/// Represents a point in time when changes was last observed. If the value
	/// changes, then it is an indication that the collection data has been
	/// modified.
	/// </summary>
	internal nuint DataMark = DataMarkInit;

	private KokoroSqliteDb? _Db;
	public KokoroSqliteDb? Db => _Db;
	public KokoroSqliteDb OwnerDb => _Db ?? _Owner.Db;
	public KokoroSqliteDb? OwnerDbOrNull => _Db ?? _Owner.DbOrNull;

	private readonly KokoroCollection _Owner;
	internal KokoroCollection Owner => _Owner;

	private InvalidationSource? _Latest;
	public InvalidationSource Latest => _Latest ?? _Owner.InvalidationSource;
	public InvalidationSource? LatestOrNull => _Latest ?? _Owner.InvalidationSource;

	internal InvalidationSource(KokoroSqliteDb db, KokoroCollection owner) {
		_Db = db;
		_Owner = owner;
		_Latest = this;
	}

	public void Dispose() {
		DataMark = DataMarkExhausted;
		_Db = null;
		_Latest = null;
	}
}
