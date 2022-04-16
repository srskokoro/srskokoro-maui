namespace Kokoro.Internal.Sqlite;
using Microsoft.Data.Sqlite;

/// <remarks>Not thread-safe.</remarks>
internal class KokoroSqliteDb : SqliteConnection {
	internal const long SqliteDbAppId = 0x1c008087L; // Hint: It's an RGBA hex

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
	}
}
