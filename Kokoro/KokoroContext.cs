using Microsoft.Data.Sqlite;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Kokoro;

public enum KokoroContextOpenMode {
	ReadWriteCreate,
	ReadWrite,
	ReadOnly,
}

/// <remarks>Not thread-safe.</remarks>
public partial class KokoroContext : IDisposable, IAsyncDisposable {

	public static int MaxSupportedVersion => KokoroCollection.OperableVersion;

	protected KokoroCollection? _collection;

	protected internal readonly KokoroSqliteDb _db;
	private readonly SqliteCommand _cmdGetVer;

	private readonly object _transactionsLock;
	private SqliteTransaction? _transactionInternal;
	private bool _transactionInternal_disposeRequested;
	private readonly HashSet<uint> _transactionSet;
	private readonly Stack<uint> _transactionStack;
	private uint _transactionKeyNext;


	public KokoroContextOpenMode Mode { get; }

	public string FullPath { get; }

	public bool IsReadOnly => Mode == KokoroContextOpenMode.ReadOnly;

	public int Version {
		get {
			_cmdGetVer.Transaction = _db.Transaction;
			return Convert.ToInt32((long)_cmdGetVer.ExecuteScalar()!);
		}
	}

	public bool IsOperable => Version == KokoroCollection.OperableVersion;

	public virtual KokoroCollection Collection {
		get {
			var r = _collection;
			if (r is null) {
				KokoroCollection.CheckIfOperable(this);
				r = new KokoroCollection(this);
				_collection = r;
			}
			return r;
		}
	}


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

		KokoroSqliteDb db = new(connStrBldr.ToString());
		db.Open();

		_db = db;
		Mode = mode;
		FullPath = path;

		_transactionsLock = _transactionSet = new();
		_transactionStack = new();

		SqliteCommand cmdGetVer = db.CreateCommand();
		cmdGetVer.CommandText = "PRAGMA user_version";
		_cmdGetVer = cmdGetVer;

		using (var transaction = db.BeginTransaction()) {
			long appId;
			using (var cmdGetAppId = db.CreateCommand()) {
				cmdGetAppId.CommandText = "PRAGMA application_id";
				appId = (long)cmdGetAppId.ExecuteScalar()!;
			}

			if (appId != 0x1c008087L) {
				if (appId != 0L) {
					throw new InvalidDataException($"SQLite database `{dbName}` found with unexpected application ID: {appId} (0x{Convert.ToString(appId, 16)})");
				}

				using (var cmdCountDbObj = db.CreateCommand()) {
					cmdCountDbObj.CommandText = "SELECT COUNT(*) FROM sqlite_schema";

					if ((long)cmdCountDbObj.ExecuteScalar()! != 0L) {
						throw new InvalidDataException($"SQLite database `{dbName}` must be empty while the application ID is zero.");
					}
				}

				{
					var v = Version;
					if (Version != 0) {
						throw new InvalidDataException($"Version (currently {v}) must be zero while the application ID is zero (for SQLite database `{dbName}`).");
					}
				}

				if (!IsReadOnly) {
					using var cmdSetAppId = db.CreateCommand();
					cmdSetAppId.CommandText = "PRAGMA application_id = 0x1c008087";
					cmdSetAppId.ExecuteNonQuery();
				}

			} else {
				var v = Version;

				if (v < 0) {
					throw new InvalidDataException($"Version (currently {v}) less than zero.");
				}

				if (v > MaxSupportedVersion) {
					throw new NotSupportedException($"Version (currently {v}) is too high.");
				}
			}

			cmdGetVer.Transaction = null;

			if (!IsReadOnly) {
				transaction.Commit();
			}
		}
	}


	public virtual KokoroTransaction BeginTransaction() {
		return new KokoroTransaction(this);
	}

	internal uint OnInitTransaction() {
		lock (_transactionsLock) {
			{
				var _ = _transactionInternal;
				if (_ is not null) {
					if (_transactionInternal_disposeRequested) {
						_.Dispose();
						_transactionInternal_disposeRequested = false;
						_transactionInternal = null;
					} else {
						goto HasTransactionInternal;
					}
				}

				// Throws if a DB transaction has already been created (externally)
				_transactionInternal = _db.BeginTransaction();
			}

		HasTransactionInternal:
			try {
				var key = unchecked(++_transactionKeyNext);
				// ^ cannot be `0` until overflow happens
				if (key == 0) {
					// Avoid `0` to reserve it for this special case
					key = unchecked(++_transactionKeyNext);

					Trace.Fail(
						$"`{nameof(KokoroTransaction)}` creation exhausted!",

						$"`{nameof(KokoroTransaction)}` creation already exhausted. It is recommended that " +
						$"a new instance of `{nameof(KokoroContext)}` be created instead for the purposes of " +
						$"starting new transactions."
					);
				}

				if (!_transactionSet.Add(key)) {
					throw new InvalidOperationException("Too many active transactions! Current count: " + _transactionStack.Count);
				}
				try {
					_transactionStack.Push(key); // May throw due to OOM for example
				} catch {
					_transactionSet.Remove(key);
					if (_transactionStack.TryPeek(out var top) && top == key) {
						_transactionStack.Pop();
					}
					throw;
				}

				return key;
			} catch {
				if (_transactionStack.Count == 0) {
					_transactionInternal!.Dispose();
					_transactionInternal = null;
				}
				throw;
			}
		}
	}

	private bool CompleteTransaction(KokoroTransaction transaction, bool throwIfCompleted = false) {
		var key = transaction._key;
		var set = _transactionSet;
		if (!set.Remove(key)) {
			if (throwIfCompleted) {
				Debug.Assert(new Func<bool>(() => {
					var _ = transaction.GetContextOrNull();
					return _ is null || _ == this;
				})());
				throw KokoroTransaction.MakeTransactionCompletedException();
			}
			return false;
		}

		var stack = _transactionStack;
		while (stack.TryPop(out var popped) && popped != key) {
			set.Remove(popped);
		}

		return stack.Count == 0;
	}

	internal void OnCommitTransaction(KokoroTransaction transaction) {
		lock (_transactionsLock) {
			if (CompleteTransaction(transaction, throwIfCompleted: true)) {
				var _ = _transactionInternal;
				if (_ is not null) {
					_.Commit();
					_transactionInternal_disposeRequested = false;
					_transactionInternal = null;
				}
			}
		}
	}

	internal void OnRollbackTransaction(KokoroTransaction transaction) {
		lock (_transactionsLock) {
			if (CompleteTransaction(transaction, throwIfCompleted: true)) {
				var _ = _transactionInternal;
				if (_ is not null) {
					_.Rollback();
					_transactionInternal_disposeRequested = false;
					_transactionInternal = null;
				}
			}
		}
	}

	internal void OnDisposeTransaction(KokoroTransaction transaction, bool disposing) {
		lock (_transactionsLock) {
			if (!CompleteTransaction(transaction))
				return;

			var _ = _transactionInternal;
			if (_ is not null) {
				if (disposing) {
					_.Dispose();
					_transactionInternal_disposeRequested = false;
					_transactionInternal = null;
				} else {
					_transactionInternal_disposeRequested = true;
					// ^ Should dispose even if `disposing == true` to prevent
					// leaking an undisposed DB transaction that can never be
					// disposed (unless the DB connection is closed).
				}
			}
		}
	}


	public virtual void MigrateToVersion(int newVersion) {
		if (IsReadOnly)
			throw new NotSupportedException("Read-only");

		if (newVersion < 0)
			throw new ArgumentOutOfRangeException(nameof(newVersion), "New version cannot be less than zero.");

		if (newVersion > MaxSupportedVersion)
			throw new ArgumentOutOfRangeException(nameof(newVersion), $"New version cannot be greater than `{nameof(MaxSupportedVersion)}` (currently {MaxSupportedVersion}).");

		if (_collection is not null)
			throw new InvalidOperationException($"Cannot migrate once `{nameof(Collection)}` is already accessed.");

		using var transaction = _db.BeginTransaction();
		var oldVersion = Version;

		if (oldVersion != newVersion) {
			if (oldVersion < newVersion) {
				OnUpgrade(oldVersion, newVersion);
			} else {
				OnDowngrade(oldVersion, newVersion);
			}

			using var cmdSetVer = _db.CreateCommand();
			cmdSetVer.CommandText = $"PRAGMA user_version = {newVersion}";
			cmdSetVer.ExecuteNonQuery();
		}

		transaction.Commit();
	}

	public void MigrateToOperableVersion() => MigrateToVersion(KokoroCollection.OperableVersion);

	public KokoroCollection ForceOperableCollection() {
		MigrateToOperableVersion();
		return Collection;
	}

	// --

	protected virtual void OnUpgrade(int oldVersion, int newVersion) {
		int curVersion = oldVersion;

		var actions = MigrationMap.Actions;
		var keys = MigrationMap.Keys;

		while (curVersion < newVersion) {
			int i = Array.BinarySearch(keys, (curVersion, newVersion));

			int target;
			if (i < 0) {
				i = ~i - 1;
				if (i < 0)
					ThrowExpectedMigrationNotImplemented(curVersion, newVersion);

				(int from, target) = keys[i];
				if (from != curVersion || from >= target)
					ThrowExpectedMigrationNotImplemented(curVersion, newVersion);
			} else {
				(_, target) = keys[i];
			}

			actions[i](this);
			curVersion = target;
		}
	}

	protected virtual void OnDowngrade(int oldVersion, int newVersion) {
		int curVersion = oldVersion;

		var actions = MigrationMap.Actions;
		var keys = MigrationMap.Keys;
		int len = keys.Length;

		while (curVersion > newVersion) {
			int i = Array.BinarySearch(keys, (curVersion, newVersion));

			int target;
			if (i < 0) {
				i = ~i;
				if (i >= len)
					ThrowExpectedMigrationNotImplemented(curVersion, newVersion);

				(int from, target) = keys[i];
				if (from != curVersion || from <= target)
					ThrowExpectedMigrationNotImplemented(curVersion, newVersion);
			} else {
				(_, target) = keys[i];
			}

			actions[i](this);
			curVersion = target;
		}
	}

	private static class MigrationMap {
		internal static readonly (int FromVersion, int ToVersion)[] Keys;
		internal static readonly Action<KokoroContext>[] Actions;

		static MigrationMap() {
			var list = ProvideMigrationMap();
			Keys = list.Keys.ToArray();
			Actions = list.Values.ToArray();
		}
	}

	private static partial SortedList<(int, int), Action<KokoroContext>> ProvideMigrationMap();

	[DoesNotReturn]
	private static void ThrowExpectedMigrationNotImplemented(int fromVersion, int toVersion) {
		throw new NotImplementedException($"Expected migration from {fromVersion} to {toVersion} is apparently not implemented.");
	}

	// --

	private bool _disposed;

	public bool IsDisposed => _disposed;

	// https://docs.microsoft.com/en-us/dotnet/standard/garbage-collection/implementing-dispose
	protected virtual void Dispose(bool disposing) {
		if (_disposed) return;

		if (disposing) {
			// Dispose managed state (managed objects)
			_db.Dispose();
		}
		// Free unmanaged resources (unmanaged objects) and override finalizer
		// Set large fields to null

		_disposed = true;
	}

	// Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
	~KokoroContext() => Dispose(false);

	public void Dispose() {
		// Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
		Dispose(true);
		GC.SuppressFinalize(this);
	}

#pragma warning disable CA1816 // Dispose methods should call SuppressFinalize
	public virtual ValueTask DisposeAsync() {
		Dispose();
		return default;
	}
#pragma warning restore CA1816
}
