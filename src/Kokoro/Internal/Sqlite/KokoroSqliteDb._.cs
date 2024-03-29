﻿namespace Kokoro.Internal.Sqlite;
using Kokoro.Internal;
using Microsoft.Data.Sqlite;
using System.ComponentModel;
using static SQLitePCL.raw;

/// <remarks>Not thread-safe.</remarks>
internal sealed partial class KokoroSqliteDb : SqliteConnectionBase {

	public bool HasActiveTransaction {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get {
			// NOTE: Auto-commit is unset (zero) while a transaction is active.
			if (sqlite3_get_autocommit(Handle) == 0) {
				return true;
			}
			return false;
		}
	}

	public KokoroSqliteDb() : this(null) { }

	public KokoroSqliteDb(string? connectionString) : base(connectionString) { }

	#region `BeginTransaction(…)` overrides
	// NOTE: We disallowed `BeginTransaction(…)` in the base class so that we
	// can set up custom hooks for commits and rollbacks.
	//
	// Here, we hid various `BeginTransaction(…)` overloads (via `new` modifier)
	// so that we can annotate them with `[Obsolete(…, error: true)]` attribute.

	[Obsolete("Not supported", error: true)][EditorBrowsable(EditorBrowsableState.Never)] public new SqliteTransaction BeginTransaction() => E_BeginTransaction_NS();
	[Obsolete("Not supported", error: true)][EditorBrowsable(EditorBrowsableState.Never)] public new SqliteTransaction BeginTransaction(bool deferred) => E_BeginTransaction_NS();
	[Obsolete("Not supported", error: true)][EditorBrowsable(EditorBrowsableState.Never)] public new SqliteTransaction BeginTransaction(System.Data.IsolationLevel isolationLevel) => E_BeginTransaction_NS();
	[Obsolete("Not supported", error: true)][EditorBrowsable(EditorBrowsableState.Never)] public new SqliteTransaction BeginTransaction(System.Data.IsolationLevel isolationLevel, bool deferred) => E_BeginTransaction_NS();

	#endregion

	[Conditional("DEBUG")]
	private static void DAssert_ConnectionString(string connectionString) {
		SqliteConnectionStringBuilder csb = new(connectionString);
		Debug.Assert(!csb.Pooling,
			"We're supposedly doing our own pooling; thus, `Pooling` should " +
			"be `False` in the connection string (yet it isn't).");
		Debug.Assert(csb.RecursiveTriggers, "Should've enabled recursive " +
			"triggers in the connection string (as it isn't).");
	}

	[Conditional("DEBUG")]
	private static void DAssert_Open(KokoroSqliteDb db) {
		Debug.Assert(db.ExecScalar<long>("PRAGMA foreign_keys") == 1);
	}

	public override void Open() {
		DAssert_ConnectionString(ConnectionString);
		base.Open();

		this.Exec("PRAGMA temp_store=FILE");
#if !DEBUG
		this.Exec("PRAGMA ignore_check_constraints=1");
#endif
		sqlite3_rollback_hook(Handle, OnSqliteRollback, null);
		sqlite3_commit_hook(Handle, OnSqliteCommit, null);

		DAssert_Open(this);
	}

	public override void Close() {
		sqlite3_commit_hook(Handle, null, null);
		sqlite3_rollback_hook(Handle, null, null);
		base.Close();
	}

	// --

	internal KokoroContext? Context;
	internal KokoroCollection? Owner;

	private long _LastPragmaDataVersion;
	internal InvalidationSource? InvalidationSource;

	internal void SetUpWith(KokoroContext context, KokoroCollection owner) {
		InvalidationSource = new(this, Owner = owner);
		Context = context;
	}

	internal void TearDown() {
		if (HasActiveTransaction) {
			this.Exec("ROLLBACK");
		}

		Context = null;
		Owner = null;

		var invsrc = InvalidationSource;
		InvalidationSource = null;

		Debug.Assert(invsrc != null);
		invsrc.Dispose();
	}

	internal bool ReloadCaches() {
		// TODO Use (and reuse) `sqlite3_stmt` directly with `SQLITE_PREPARE_PERSISTENT`
		// - See, for example, how SQLite did it for its FTS5 extension, https://github.com/sqlite/sqlite/blob/2d27d36cba01b9ceff2c36ad0cef9468db370024/ext/fts5/fts5_index.c#L1066
		// - Once the above TODO is done, consider marking this method for
		// aggressive inlining.
		var currentPragmaDataVersion = this.ExecScalar<long>("PRAGMA data_version");
		if (currentPragmaDataVersion == _LastPragmaDataVersion) return false;

		Invalidate();
		_LastPragmaDataVersion = currentPragmaDataVersion;

		ClearCaches();
		return true;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void Invalidate() {
		if (++InvalidationSource!.DataMark == InvalidationSource.DataMarkExhausted)
			OnDataMarkExhausted();

		// Placed on a separate function to reduce method size, since the
		// following is for a very rare case anyway; and also, to allow the
		// outer method to be inlined (since, as of writing this, methods
		// containing try-catch blocks cannot be inlined).
		void OnDataMarkExhausted() {
			InvalidationSource prev = InvalidationSource!, next;
			try {
				next = new(this, Owner!); // May throw due to OOM
			} catch {
				--prev.DataMark; // Revert the increment
				throw;
			}
			InvalidationSource = next;
			prev.Dispose();
		}
	}

	// --

	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	[SkipLocalsInit]
	private int OnSqliteCommit(object user_data) {
		try {
			Invalidate();
		} catch {
#if DEBUG
			throw;
#endif
		}
		return 0;
	}

	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	[SkipLocalsInit]
	private void OnSqliteRollback(object user_data) {
		try {
			ClearCaches();
		} catch {
#if DEBUG
			throw;
#endif
		}
	}

	internal void OnNestingWriteRollback() => ClearCaches();

	internal void ClearCaches() {
		// NOTE: We should ensure that the name id caches are never stale by
		// preventing name entries from being remapped to a different rowid so
		// long as the DB or context exists. One way to accomplish that is to
		// clear the cache on rollback, and delete any offending cache entry
		// whenever a name entry is deleted (as the rowid might get remapped).
		ClearNameIdCaches();
	}
}
