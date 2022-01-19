using Microsoft.Data.Sqlite;
using System.Diagnostics.CodeAnalysis;

namespace Kokoro;

public enum KokoroContextOpenMode {
	ReadWriteCreate,
	ReadWrite,
	ReadOnly,
}

/// <remarks>Not thread safe.</remarks>
public partial class KokoroContext : IDisposable, IAsyncDisposable {

	public static int MaxSupportedVersion => KokoroCollection.OperableVersion;

	private KokoroCollection? _collection;

	protected internal readonly KokoroSqliteDb _db;
	private readonly SqliteCommand _cmdGetVer;

	private SqliteTransaction? _dbTransaction;

	private readonly HashSet<object> _transactionSet;
	private readonly Stack<object> _transactionStack;
	private readonly object _transactionsLock;


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


	public KokoroContext(string path, KokoroContextOpenMode mode) {
		path = Path.GetFullPath(path);

		string dbPath = Path.Combine(path, "collection.db");
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

		SqliteCommand cmdGetVer = db.CreateCommand();
		cmdGetVer.CommandText = "PRAGMA user_version";
		_cmdGetVer = cmdGetVer;

		var v = Version;
		if (v < 0) {
			throw new InvalidDataException($"Version (currently {v}) less than zero.");
		}
		if (v > MaxSupportedVersion) {
			throw new NotSupportedException($"Version (currently {v}) is too high.");
		}

		_db = db;
		Mode = mode;
		FullPath = path;

		_transactionsLock = _transactionSet = new();
		_transactionStack = new();
	}


	public virtual KokoroTransaction BeginTransaction() {
		return new KokoroTransaction(this);
	}

	internal void OnInitTransaction(KokoroTransaction transaction) {
		lock (_transactionsLock) {
			if (_dbTransaction is null) {
				_dbTransaction = _db.BeginTransaction();
			}

			try {
				var key = transaction._key;
				_transactionSet.Add(key); // May fail due to OOM for example
				_transactionStack.Push(key);
			} catch {
				if (_transactionStack.Count == 0) {
					_dbTransaction.Dispose();
					_dbTransaction = null;
				}
				// Dispose here so that finalizer doesn't have to acquire a separate lock
				transaction.Dispose();

				throw;
			}
		}
	}

	private bool RemoveTransaction(KokoroTransaction transaction) {
		var key = transaction._key;
		var set = _transactionSet;
		if (!set.Remove(key)) {
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
			if (RemoveTransaction(transaction)) {
				_dbTransaction?.Commit();
				_dbTransaction = null;
			}
		}
	}

	internal void OnRollbackTransaction(KokoroTransaction transaction) {
		lock (_transactionsLock) {
			if (RemoveTransaction(transaction)) {
				_dbTransaction?.Rollback();
				_dbTransaction = null;
			}
		}
	}

	internal void OnDisposeTransaction(KokoroTransaction transaction) {
		lock (_transactionsLock) {
			if (RemoveTransaction(transaction)) {
				_dbTransaction?.Dispose();
				_dbTransaction = null;
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

			SqliteCommand cmdSetVer = _db.CreateCommand();
			cmdSetVer.CommandText = "PRAGMA user_version = $newVersion";
			cmdSetVer.Parameters.AddWithValue("$newVersion", newVersion);
			cmdSetVer.ExecuteNonQuery();
		}

		transaction.Commit();
	}

	public void MigrateToOperableVersion() => MigrateToVersion(KokoroCollection.OperableVersion);

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
