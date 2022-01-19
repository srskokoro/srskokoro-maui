using Microsoft.Data.Sqlite;

namespace Kokoro;

public class KokoroSqliteDb : SqliteConnection {

	public new SqliteTransaction? Transaction { get => base.Transaction; }

	public KokoroSqliteDb() : base() { }

	public KokoroSqliteDb(string? connectionString) : base(connectionString) { }
}
