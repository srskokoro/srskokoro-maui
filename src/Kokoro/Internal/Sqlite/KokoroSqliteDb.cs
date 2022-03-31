﻿namespace Kokoro.Internal.Sqlite;
using Microsoft.Data.Sqlite;

/// <remarks>Not thread-safe.</remarks>
internal class KokoroSqliteDb : SqliteConnection {
	internal const long SqliteDbAppId = 0x1c008087L; // Hint: It's an RGBA hex

	public new SqliteTransaction? Transaction { get => base.Transaction; }

	public KokoroSqliteDb() : this(null) { }

	public KokoroSqliteDb(string? connectionString) : base(connectionString) { }

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public KokoroSqliteDb OpenAndGet() { Open(); return this; }
}
