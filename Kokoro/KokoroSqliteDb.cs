using Kokoro.Util;
using Microsoft.Data.Sqlite;
using System.Runtime.CompilerServices;

namespace Kokoro;

/// <remarks>Not thread-safe.</remarks>
public class KokoroSqliteDb : SqliteConnection {

	internal readonly SqliteCommand CmdGetVersion;

	public int Version {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => Convert.ToInt32(CmdGetVersion.In(Transaction).ExecuteScalar<long>());
	}

	public new SqliteTransaction? Transaction { get => base.Transaction; }

	public KokoroSqliteDb() : this(null) { }

	public KokoroSqliteDb(string? connectionString) : base(connectionString) {
		CmdGetVersion = this.CreateCommand("PRAGMA user_version");
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public KokoroSqliteDb OpenAndGet() { Open(); return this; }
}
