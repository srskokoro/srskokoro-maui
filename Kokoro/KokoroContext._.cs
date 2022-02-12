using Kokoro.Util;
using Microsoft.Data.Sqlite;
using System.Collections.Concurrent;

namespace Kokoro;

public partial class KokoroContext : IDisposable, IAsyncDisposable {
	private const long SqliteDbAppId = 0x1c008087L;

	public KokoroContextOpenMode Mode { get; }

	public string FullPath { get; }

	public bool IsReadOnly => Mode == KokoroContextOpenMode.ReadOnly;

	public int Version {
		[MethodImpl(MethodImplOptions.AggressiveOptimization)]
		get {
			using var rr = Get<KokoroSqliteDb>();
			return rr.Value.Version;
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
			db.DisposePriorThrow(ex);
			throw;
		}

		Return(db); // Initial pool entry
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


	#region Internal `SqliteConnection`

	private readonly string _DbConnectionString;

	private readonly ConcurrentBag<KokoroSqliteDb> _DbPool = new();
	// Increment "before" adding to pool. Decrement "after" taking from pool.
	// Should only be considered as a snapshot (and not the actual size).
	private int _DbPoolSize = 0;

	#endregion

	#region `Borrow` and `Return` mechanics

	private static readonly int _ProposedPoolingMax = Math.Max(Environment.ProcessorCount * 2, 4);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public ReturnHandler<T> Get<T>() where T : IDisposable {
		return new(this, Borrow<T>());
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public ReturnHandler<T> Get<T>(out T borrowed) where T : IDisposable {
		return new(this, borrowed = Borrow<T>());
	}

	public virtual T Borrow<T>() where T : IDisposable {
		if (Volatile.Read(ref _DisposeRequested) == true) {
			throw E_Disposed(); // Already disposed or being disposed
		}

		if (typeof(T) == typeof(KokoroSqliteDb)) {
			if (_DbPool.TryTake(out var result)) {
				Interlocked.Decrement(ref _DbPoolSize);
			} else {
				result = new KokoroSqliteDb(_DbConnectionString);
				result.Open();
			}
			return (T)(object)result;
		} else {
			throw new NotSupportedException();
		}
	}

	/// <remarks>
	/// WARNING: Do not return a borrowed entry more than once. Duplicate
	/// returns may cause duplicate pool entries.
	/// </remarks>
	public virtual void Return<T>(T borrowed) where T : IDisposable {
		if (typeof(T) == typeof(KokoroSqliteDb)) {
			if (_DbPoolSize >= _ProposedPoolingMax) {
				goto Reject;
			}
			Interlocked.Increment(ref _DbPoolSize);
			_DbPool.Add((KokoroSqliteDb)(object)borrowed);
		} else {
			throw new NotSupportedException();
		}

		if (Volatile.Read(ref _DisposeRequested) != true) {
			return; // Not yet disposed. We're good then.
		}

	Reject:
		// Either we're already disposed or the pool designated for the
		// borrowed entry has reached its maximum size. If we're already
		// disposed, `Return()` should not throw -- it should still simply
		// reject returns silently.
		borrowed.Dispose();
	}

	public ref struct ReturnHandler<T> where T : IDisposable {
		private KokoroContext? _Context;
		private T _Value;

		public readonly KokoroContext Context {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => _Context ?? throw E_Disposed();
		}

		public readonly T Value {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => _Value!;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal ReturnHandler(KokoroContext context, T borrowed) {
			_Context = context;
			_Value = borrowed;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Return() {
			Context.Return(_Value);
			_Context = null;
			_Value = default!;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Dispose() => Return();

		private static ObjectDisposedException E_Disposed()
			=> DisposeUtil.Ode(typeof(ReturnHandler<T>));
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
				var dbPool = _DbPool;
				while (dbPool.TryTake(out var db)) {
					db.Dispose();
				}
			}

			// Here we should free unmanaged resources (unmanaged objects),
			// override finalizer, and set large fields to null
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
