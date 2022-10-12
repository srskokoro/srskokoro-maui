namespace Kokoro.Internal.Sqlite;
using Microsoft.Data.Sqlite;
using System.ComponentModel;

internal abstract class SqliteConnectionBase : SqliteConnection {

	public SqliteConnectionBase(string? connectionString) : base(connectionString) { }

	#region `BeginTransaction(…)` overrides
	// NOTE: We disallowed `BeginTransaction(…)` so that we can set up custom
	// hooks for commits and rollbacks.

#pragma warning disable CS0809 // Obsolete member overrides non-obsolete member
	[Obsolete("Not supported", error: true)][EditorBrowsable(EditorBrowsableState.Never)] public sealed override SqliteTransaction BeginTransaction() => E_BeginTransaction_NS();
	[Obsolete("Not supported", error: true)][EditorBrowsable(EditorBrowsableState.Never)] public sealed override SqliteTransaction BeginTransaction(bool deferred) => E_BeginTransaction_NS();
	[Obsolete("Not supported", error: true)][EditorBrowsable(EditorBrowsableState.Never)] public sealed override SqliteTransaction BeginTransaction(System.Data.IsolationLevel isolationLevel) => E_BeginTransaction_NS();
	[Obsolete("Not supported", error: true)][EditorBrowsable(EditorBrowsableState.Never)] public sealed override SqliteTransaction BeginTransaction(System.Data.IsolationLevel isolationLevel, bool deferred) => E_BeginTransaction_NS();
#pragma warning restore CS0809

	[EditorBrowsable(EditorBrowsableState.Never)]
	[DoesNotReturn]
	private protected static SqliteTransaction E_BeginTransaction_NS() => throw new NotSupportedException();

	#endregion
}
