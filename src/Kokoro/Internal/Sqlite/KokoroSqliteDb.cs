namespace Kokoro.Internal.Sqlite;
using Kokoro.Internal;
using Microsoft.Data.Sqlite;

/// <remarks>Not thread-safe.</remarks>
internal class KokoroSqliteDb : SqliteConnection {

	public new SqliteTransaction? Transaction { get => base.Transaction; }

	public KokoroSqliteDb() : this(null) { }

	public KokoroSqliteDb(string? connectionString) : base(connectionString) { }

	public override void Open() {
		Debug.Assert(!new SqliteConnectionStringBuilder(ConnectionString).Pooling,
			"We're supposedly doing our own pooling; thus, `Pooling` should be " +
			"`False` in the connection string (yet it isn't).");

		base.Open();

#if !DEBUG
		this.Exec("PRAGMA ignore_check_constraints=1");
#endif
		this.Exec("PRAGMA temp_store=FILE");
	}

	// --

	private KokoroCollection? _CurrentOwner;
	internal KokoroCollection? CurrentOwner {
		get => _CurrentOwner;
		set {
			_CurrentOwner = value;
			_DataToken = value != null ? new(value) : null;
		}
	}

	private long _LastPragmaDataVersion;
	internal DataToken? _DataToken;

	internal bool UpdateDataToken() {
		// TODO Use (and reuse) `sqlite3_stmt` directly with `SQLITE_PREPARE_PERSISTENT`
		// - See, for example, how SQLite did it for its FTS5 extension, https://github.com/sqlite/sqlite/blob/2d27d36cba01b9ceff2c36ad0cef9468db370024/ext/fts5/fts5_index.c#L1066
		var currentPragmaDataVersion = this.ExecScalar<long>("PRAGMA data_version");
		if (currentPragmaDataVersion == _LastPragmaDataVersion) return false;

		_LastPragmaDataVersion = currentPragmaDataVersion;
		if (++_DataToken!.DataMark == DataToken.DataMarkExhausted) {
			_DataToken = new(_CurrentOwner!);
		}
		return true;
	}
}
