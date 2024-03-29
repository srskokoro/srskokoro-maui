﻿namespace Kokoro.Internal.Sqlite;

internal ref struct NestingWriteTransaction {
	private KokoroSqliteDb? _Db;
	private readonly string _CommitCommand;

	private const string OutermostCommit = "END";
	private readonly bool IsOutermost {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => ReferenceEquals(_CommitCommand, OutermostCommit);
	}

	public NestingWriteTransaction(KokoroSqliteDb db) {
		string init;
		if (!db.HasActiveTransaction) {
			// NOTE: We're also currently the outermost transaction.
			init = "BEGIN IMMEDIATE";
			_CommitCommand = OutermostCommit;
		} else {
			init = "SAVEPOINT _";
			// ^ NOTE: The SQLite docs explicitly say that "transaction names
			// need not be unique" and that only the *most recent* savepoint
			// with a matching name is released or rolled back; in other words,
			// we're allowed to use the same savepoint name even if there's
			// already an outer savepoint using the same name. It also means
			// that we can use any savepoint name with practically nothing to
			// worry about. See, https://www.sqlite.org/lang_savepoint.html
			_CommitCommand = "RELEASE _";
		}

		// TODO Use (and reuse) `sqlite3_stmt` directly with `SQLITE_PREPARE_PERSISTENT`
		db.Exec(init);
		_Db = db;
	}

	public void Commit() {
		try {
			// TODO Use (and reuse) `sqlite3_stmt` directly with `SQLITE_PREPARE_PERSISTENT`
			_Db!.Exec(_CommitCommand);
		} catch (NullReferenceException) when (_Db == null) {
			E_AlreadyReleased_InvOp();
		}
		_Db = null;
	}

	public void Dispose() {
		var db = _Db;
		if (db == null) return;

		// Make sure that the current transaction is still active and wasn't
		// rolled back automatically due to an error.
		//
		// See, https://www.sqlite.org/lang_transaction.html#response_to_errors_within_a_transaction:~:text=Response%20To%20Errors%20Within%20A%20Transaction
		//
		if (db.HasActiveTransaction) {
			// TODO Use (and reuse) `sqlite3_stmt` directly with `SQLITE_PREPARE_PERSISTENT`
			if (IsOutermost) {
				db.Exec("ROLLBACK");
			} else {
				db.Exec("ROLLBACK TO _; RELEASE _");
				db.OnNestingWriteRollback();
			}
		}
		_Db = null;
	}

	public void DisposeNoInvalidate() {
		var db = _Db;
		if (db == null) return;

		// Same as `Dispose()` above, but avoids clearing caches.
		if (db.HasActiveTransaction) {
			// TODO Use (and reuse) `sqlite3_stmt` directly with `SQLITE_PREPARE_PERSISTENT`
			db.Exec(!IsOutermost ? "ROLLBACK TO _; RELEASE _" : "ROLLBACK");
		}
		_Db = null;
	}

	[DoesNotReturn]
	private static void E_AlreadyReleased_InvOp()
		=> throw new InvalidOperationException($"Transaction already complete; it's no longer usable.");
}
