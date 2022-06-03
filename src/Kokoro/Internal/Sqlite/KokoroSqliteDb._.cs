namespace Kokoro.Internal.Sqlite;
using Kokoro.Internal;
using Microsoft.Data.Sqlite;

/// <remarks>Not thread-safe.</remarks>
internal partial class KokoroSqliteDb : SqliteConnection {

	public new SqliteTransaction? Transaction { get => base.Transaction; }

	public KokoroSqliteDb() : this(null) { }

	public KokoroSqliteDb(string? connectionString) : base(connectionString) { }

	public override void Open() {
		Debug.Assert(!new SqliteConnectionStringBuilder(ConnectionString).Pooling,
			"We're supposedly doing our own pooling; thus, `Pooling` should be " +
			"`False` in the connection string (yet it isn't).");

		base.Open();

		this.Exec("PRAGMA temp_store=FILE");
#if !DEBUG
		this.Exec("PRAGMA ignore_check_constraints=1");
#endif
	}

	// --

	internal KokoroContext? Context;
	internal KokoroCollection? Owner;

	private long _LastPragmaDataVersion;
	internal InvalidationSource? InvSrc;

	internal void SetUpWith(KokoroContext context, KokoroCollection owner) {
		InvSrc = new(this, Owner = owner);
		Context = context;
	}

	internal void TearDown() {
		Context = null;
		Owner = null;

		var invsrc = InvSrc;
		InvSrc = null;

		Debug.Assert(invsrc != null);
		invsrc.Dispose();
	}

	internal bool UpdateInvSrc() {
		// TODO Use (and reuse) `sqlite3_stmt` directly with `SQLITE_PREPARE_PERSISTENT`
		// - See, for example, how SQLite did it for its FTS5 extension, https://github.com/sqlite/sqlite/blob/2d27d36cba01b9ceff2c36ad0cef9468db370024/ext/fts5/fts5_index.c#L1066
		// - Once the above TODO is done, consider marking this method for
		// aggressive inlining.
		var currentPragmaDataVersion = this.ExecScalar<long>("PRAGMA data_version");
		if (currentPragmaDataVersion == _LastPragmaDataVersion) return false;

		if (++InvSrc!.DataMark == InvalidationSource.DataMarkExhausted)
			OnDataMarkExhausted();

		_LastPragmaDataVersion = currentPragmaDataVersion;
		return true;

		// Placed on a separate function to reduce method size, since the
		// following is for a very rare case anyway; and also, to allow the
		// outer method to be inlined (since, as of writing this, methods
		// containing try-catch blocks cannot be inlined).
		void OnDataMarkExhausted() {
			InvalidationSource prev = InvSrc!, next;
			try {
				next = new(this, Owner!); // May throw due to OOM
			} catch {
				--prev.DataMark; // Revert the increment
				throw;
			}
			InvSrc = next;
			prev.Dispose();
		}
	}
}
