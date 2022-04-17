namespace Kokoro.Internal.Sqlite;
using Microsoft.Data.Sqlite;

/// <remarks>Not thread-safe.</remarks>
internal class KokoroSqliteDb : SqliteConnection {

	public new SqliteTransaction? Transaction { get => base.Transaction; }

	public KokoroSqliteDb() : this(null) { }

	public KokoroSqliteDb(string? connectionString) : base(connectionString) { }

	public override void Open() {
		base.Open();
#if !DEBUG
		Debug.Assert(!new SqliteConnectionStringBuilder(ConnectionString).Pooling,
			"We're supposedly doing our own pooling; thus, `Pooling` should be " +
			"`False` in the connection string (yet it isn't).");

		this.ExecuteNonQuery("PRAGMA ignore_check_constraints=1");
#endif
		this.ExecuteNonQuery("PRAGMA temp_store=FILE");
	}
}
