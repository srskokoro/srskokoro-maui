namespace Kokoro.Internal.Sqlite;
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
}
