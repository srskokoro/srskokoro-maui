namespace Kokoro.Internal.Sqlite;
using Microsoft.Data.Sqlite;

internal abstract class SqliteConnectionBase : SqliteConnection {

	public SqliteConnectionBase(string? connectionString) : base(connectionString) { }

	#region `BeginTransaction(…)` overrides
	// NOTE: We disallowed `BeginTransaction(…)` so that we can set up custom
	// hooks for commits and rollbacks.

	public sealed override SqliteTransaction BeginTransaction() => E_BeginTransaction_NS();
	public sealed override SqliteTransaction BeginTransaction(bool deferred) => E_BeginTransaction_NS();
	public sealed override SqliteTransaction BeginTransaction(System.Data.IsolationLevel isolationLevel) => E_BeginTransaction_NS();
	public sealed override SqliteTransaction BeginTransaction(System.Data.IsolationLevel isolationLevel, bool deferred) => E_BeginTransaction_NS();

	[DoesNotReturn]
	private protected static SqliteTransaction E_BeginTransaction_NS() => throw new NotSupportedException();

	#endregion
}
