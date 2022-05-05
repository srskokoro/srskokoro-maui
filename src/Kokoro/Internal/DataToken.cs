namespace Kokoro.Internal;
using Kokoro.Internal.Sqlite;

/// <summary>
/// A lightweight handle to the collection data that also keeps track of a
/// certain "<see cref="DataMark">data mark</see>" useful for detecting changes
/// to the underlying collection data.
/// <para>
/// This object is always directly tied to a <see cref="KokoroCollection"/>
/// object, with disposal of the latter object meant to cause disposal of this
/// object as well.
/// </para>
/// </summary>
internal sealed class DataToken : IDisposable {
	internal const nuint DataMarkExhausted = 0;
	internal const nuint DataMarkInit = 1;

	/// <summary>
	/// Represents a point in time when changes was last observed. If the value
	/// changes, then it is an indication that the collection data has been
	/// modified.
	/// </summary>
	internal nuint DataMark = DataMarkInit;

	private KokoroSqliteDb? _Db;
	private KokoroContext? _Context;

	internal KokoroSqliteDb? Db => _Db;
	internal KokoroContext? Context => _Context;

	internal KokoroSqliteDb OwnerDb => _Db ?? _Owner.Db;
	internal KokoroSqliteDb? OwnerDbOrNull => _Db ?? _Owner.DbOrNull;

	internal KokoroContext OwnerContext => _Context ?? _Owner.Context;
	internal KokoroContext? OwnerContextOrNull => _Context ?? _Owner.ContextOrNull;

	private readonly KokoroCollection _Owner;
	internal KokoroCollection Owner => _Owner;

	private DataToken? _Latest;
	internal DataToken Latest => _Latest ?? _Owner.DataToken;
	internal DataToken? LatestOrNull => _Latest ?? _Owner.DataTokenOrNull;

	internal DataToken(KokoroSqliteDb db, KokoroContext context, KokoroCollection owner) {
		_Db = db;
		_Context = context;
		_Owner = owner;
		_Latest = this;
	}

	public void Dispose() {
		DataMark = DataMarkExhausted;
		_Db = null;
		_Context = null;
		_Latest = null;
	}
}
