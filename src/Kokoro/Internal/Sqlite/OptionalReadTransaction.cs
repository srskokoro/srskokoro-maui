namespace Kokoro.Internal.Sqlite;

internal ref struct OptionalReadTransaction {
	private KokoroSqliteDb? _Db;

	public OptionalReadTransaction(KokoroSqliteDb db) {
		if (!db.HasActiveTransaction) {
			// TODO Use (and reuse) `sqlite3_stmt` directly with `SQLITE_PREPARE_PERSISTENT`
			db.Exec("BEGIN");
			_Db = db;
		} else {
			_Db = null;
		}
	}

	public void Dispose() {
		var db = _Db;
		if (db != null) {
			// Make sure that the current transaction is still active and wasn't
			// rolled back automatically due to an error.
			//
			// See, https://www.sqlite.org/lang_transaction.html#response_to_errors_within_a_transaction:~:text=Response%20To%20Errors%20Within%20A%20Transaction
			//
			if (db.HasActiveTransaction) {
				// TODO Use (and reuse) `sqlite3_stmt` directly with `SQLITE_PREPARE_PERSISTENT`
				// - If the above TODO is done, then we won't even need to check
				// for the auto-commit flag. According to the SQLite docs (in
				// the link below), even if a ROLLBACK fails (because there's no
				// active transaction), no harm will be done.
				//   - https://www.sqlite.org/lang_transaction.html#response_to_errors_within_a_transaction:~:text=Response%20To%20Errors%20Within%20A%20Transaction
				//   - In fact, the SQLite docs even recommends rolling back
				//   when certain kinds of errors occur, even if such errors
				//   may already have rolled back the transaction automatically.
				db.Exec("ROLLBACK");
			}
			_Db = null;
		}
	}
}
