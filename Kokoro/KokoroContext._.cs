using Kokoro.Util;
using Kokoro.Util.Pooling;
using Microsoft.Data.Sqlite;

namespace Kokoro;

public partial class KokoroContext : IDisposable, IAsyncDisposable {
	private const long SqliteDbAppId = 0x1c008087L;

	public KokoroContextOpenMode Mode { get; }

	public string FullPath { get; }

	public bool IsReadOnly => Mode == KokoroContextOpenMode.ReadOnly;

	public int Version {
		[MethodImpl(MethodImplOptions.AggressiveOptimization)]
		get {
			using var h = GetThreadDb();
			return h.Db.Version;
		}
	}

	public bool IsOperable {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => Version == KokoroCollection._OperableVersion;
	}

	internal const int _MaxSupportedVersion = KokoroCollection._OperableVersion;

	public static int MaxSupportedVersion => _MaxSupportedVersion;


	public KokoroContext(string path, bool isReadOnly)
		: this(path, isReadOnly ? KokoroContextOpenMode.ReadOnly : KokoroContextOpenMode.ReadWriteCreate) { }

	public KokoroContext(string path, KokoroContextOpenMode mode) {
		const string dbName = "collection.db";
		path = Path.GetFullPath(path);

		string dbPath = Path.Combine(path, dbName);
		SqliteConnectionStringBuilder connStrBldr = new() { RecursiveTriggers = true };

		switch (mode) {
			case KokoroContextOpenMode.ReadWriteCreate: {
				Directory.CreateDirectory(path);
				connStrBldr.Mode = SqliteOpenMode.ReadWriteCreate;
				connStrBldr.DataSource = new Uri($"file:{dbPath}").ToString();
				break;
			}
			case KokoroContextOpenMode.ReadWrite: {
				DirectoryMustExist(path);
				connStrBldr.Mode = SqliteOpenMode.ReadWrite;
				connStrBldr.DataSource = new Uri($"file:{dbPath}").ToString();
				break;
			}
			case KokoroContextOpenMode.ReadOnly: {
				DirectoryMustExist(path);
				connStrBldr.Mode = SqliteOpenMode.ReadOnly;
				connStrBldr.DataSource = new Uri($"file:{dbPath}?immutable=1").ToString();
				break;
			}
		}

		static void DirectoryMustExist(string path) {
			if (!Directory.Exists(path))
				throw new DirectoryNotFoundException(path);
		}

		Mode = mode;
		FullPath = path;

		var db = new KokoroSqliteDb(_DbConnectionString = connStrBldr.ToString());
		db.Open();

		try {
			Validate(db);
		} catch (Exception ex) {
			db.DisposeSafely(ex);
			throw;
		}

		// Initial pool entry
		if (_DbPool.TryPool(db)) {
			Debug.Assert(false, "Initial pool attempt failed.");
		}
	}

	private void Validate(KokoroSqliteDb db) {
		using (var transaction = db.BeginTransaction()) {
			long appId = db.ExecuteScalar<long>("PRAGMA application_id");

			if (appId != SqliteDbAppId) {
				if (appId != 0L) {
					throw new InvalidDataException($"SQLite database found with unexpected `application_id` of {appId} (0x{Convert.ToString(appId, 16)})");
				}

				if (db.ExecuteScalar<long>("SELECT COUNT(*) FROM sqlite_schema") != 0L) {
					throw new InvalidDataException($"SQLite database must be empty while the `application_id` is zero");
				}

				var v = db.Version;
				if (v != 0) {
					throw new InvalidDataException($"Version (currently {v}) must be zero while the SQLite `application_id` is zero");
				}

				if (!IsReadOnly) {
					db.ExecuteNonQuery($"PRAGMA application_id = {SqliteDbAppId}");
				}
			} else {
				var v = db.Version;

				if (v < 0) {
					throw new InvalidDataException($"Version (currently {v}) less than zero");
				}

				if (v > _MaxSupportedVersion) {
					throw new NotSupportedException($"Version (currently {v}) is too high");
				}
			}

			db._CmdGetVersion.Transaction = null;

			if (!IsReadOnly) {
				transaction.Commit();

				db.ExecuteNonQuery("PRAGMA journal_mode = WAL");
			}
		}
	}


	#region Internal `SqliteConnection` access

	private readonly string _DbConnectionString;

	private readonly DisposingObjectPool<KokoroSqliteDb> _DbPool = new();

	private readonly ThreadLocal<DbAccess> _CurrentDbAccess = new(static () => new DbAccess());

	private class DbAccess {
		internal int _DbAccessCount;
		internal KokoroSqliteDb? _Db;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public DbDimissingHandle GetThreadDb() => new(this, AccessThreadDb());

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public DbDimissingHandle GetThreadDb(out KokoroSqliteDb db) => new(this, db = AccessThreadDb());

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public DbDimissingHandle AccessThreadDb(out KokoroSqliteDb db) => GetThreadDb(out db); // Alias

	/// <summary>Each call to this method must eventually be paired with a call
	/// to <see cref="DimissThreadDb(KokoroSqliteDb)" />.</summary>
	public KokoroSqliteDb AccessThreadDb() {
		var dba = _CurrentDbAccess.Value!;
		var db = dba._Db;
		if (db is null) {
			if (!_DbPool.TryTakeAggressively(out db)) {
				if (Volatile.Read(ref _DisposeRequested)) {
					throw E_Disposed();
				}
				db = new(_DbConnectionString);
				db.Open();
			}
			dba._Db = db;
		}
		dba._DbAccessCount++;
		return db;
	}

	/// <summary>This method must be called once (and only once) for each call
	/// to <see cref="AccessThreadDb()" />.</summary>
	public void DimissThreadDb(KokoroSqliteDb db) {
		var dba = _CurrentDbAccess.Value!;
		if (dba._Db != db) {
			throw new ArgumentException($"`{nameof(db)}` is not owned by the current thread.", nameof(db));
		}
		Debug.Assert(dba._DbAccessCount > 0);

		if (--dba._DbAccessCount <= 0) {
			dba._DbAccessCount = 0;
			dba._Db = null;
			try {
				_DbPool.TryPool(db);
			} catch (ThreadInterruptedException ex) {
				db.DisposeSafely(ex);
				throw;
			}
		}
	}

	public ref struct DbDimissingHandle {
		private KokoroContext? _Context;
		private readonly KokoroSqliteDb _Db;

		public readonly KokoroContext Context {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => _Context ?? throw E_Disposed();
		}

		public readonly KokoroSqliteDb Db {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => _Db;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal DbDimissingHandle(KokoroContext context, KokoroSqliteDb borrowed) {
			_Context = context;
			_Db = borrowed;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Dispose() {
			var ctx = _Context;
			if (ctx is not null) {
				ctx.DimissThreadDb(_Db);
				_Context = null;
			}
		}

		private static ObjectDisposedException E_Disposed()
			=> DisposeUtil.Ode(typeof(DbDimissingHandle));
	}

	#endregion

	#region `IDisposable` implementation

	private object? _DisposeLock = new(); // Unset once fully disposed
	private bool _DisposeRequested; // Set once dispossal begins

	protected internal virtual void Dispose(bool disposing) {
		var dlock = _DisposeLock;
		if (dlock == null) {
			return; // Already disposed
		}
		_DisposeRequested = true;
		// -- Memory Barrier --
		lock (dlock) {
			if (_DisposeLock == null) {
				return; // Already disposed
			}
			if (disposing) {
				// Dispose managed state (managed objects)
				// --
				_DbPool.Dispose();
			}

			// Here we should free unmanaged resources (unmanaged objects),
			// override finalizer, and set large fields to null.
			//
			// NOTE: Make sure to check for null fields, for when the
			// constructor fails to complete but the finalizer calls us.
			// --

			// Mark disposal as successful
			_DisposeLock = null;
		}
	}

	~KokoroContext() => Dispose(disposing: false);

	public void Dispose() {
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
		// ^- Side-effect: `this` is kept alive 'til the method ends
	}

	public virtual ValueTask DisposeAsync() {
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
		// ^- Side-effect: `this` is kept alive 'til the method ends
		return default;
	}

	#endregion

	private ObjectDisposedException E_Disposed()
		=> DisposeUtil.Ode(GetType());
}
