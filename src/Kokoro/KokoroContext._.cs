namespace Kokoro;
using Kokoro.Common.IO;
using Kokoro.Common.Pooling;
using Kokoro.Common.Sqlite;
using Kokoro.Internal.Sqlite;
using Microsoft.Data.Sqlite;
using Microsoft.Win32.SafeHandles;

public partial class KokoroContext : IDisposable {

	#region Constants

	#region Forward-compatible Constants

	private const string LockFile = ".kokoro.lock";

	internal const string RollbackSuffix = $".rollback";
	internal const string DraftSuffix = $"-draft";
	internal const string StaleSuffix = $"-stale";

	private const string DataDir = $"data";

	private const string DataRollbackDir = $"{DataDir}.{RollbackSuffix}";
	private const string DataRollbackDraft = $"{DataRollbackDir}{DraftSuffix}";
	private const string DataRollbackStale = $"{DataRollbackDir}{StaleSuffix}";

	/// <summary>The file holding the data schema version of the collection.</summary>
	private const string DataVersionFile = $".ver";
	private const string DataVersionDraft = $"{DataVersionFile}{DraftSuffix}";

	private const string DataVersionFileHeader = $"SRS Kokoro Collection | Schema Version ";
	private const int DataVersionFileHeader_Length = 39;

	private const string DataVersionFileHeaderZero = $"{DataVersionFileHeader}{KokoroDataVersion.ZeroString}";

	#endregion

	// NOTE: 41 is the length of the interpolated string `$"{UInt64.MaxValue}.{UInt64.MaxValue}"`
	private const int DataVersionFileHeaderWithVersion_MaxChars = 41 + DataVersionFileHeader_Length;
	private const int DataVersionFileHeaderWithVersion_MaxBytes =
		IOUtils.MaxBytesForBom + DataVersionFileHeaderWithVersion_MaxChars * IOUtils.MaxBytesPerChar;

	internal const string TrashDir = $"trash";

	#endregion

	public string BasePath { get; }

	public string DataPath { get; }

	internal string GetTrash() => Path.Join(BasePath, TrashDir);

	internal string EnsureTrash() {
		string trashPath = GetTrash();
		Directory.CreateDirectory(trashPath);
		return trashPath;
	}

	public bool IsReadOnly => Mode == KokoroContextOpenMode.ReadOnly;

	public KokoroContextOpenMode Mode { get; }

	private KokoroDataVersion _Version;
	public KokoroDataVersion Version => _Version;

	private readonly SafeFileHandle _LockHandle;
	private bool IsConstructorDone => _LockHandle != null;

	#region Construction

	[SkipLocalsInit]
	public KokoroContext(string path, KokoroContextOpenMode mode = KokoroContextOpenMode.ReadWriteCreate) {
		path = Path.GetFullPath(path);
		string dataPath = Path.Join(path, DataDir);

		Debug.Assert(!Path.EndsInDirectorySeparator(dataPath),
			$"`{nameof(DataDir)}` shouldn't end in a directory separator");

		BasePath = path;
		DataPath = dataPath;
		Mode = mode;

		SafeFileHandle? lockHandle = null;
		string lockPath = Path.Join(path, LockFile);
		string verPath = Path.Join(dataPath, DataVersionFile);

		Debug.Assert(!Path.EndsInDirectorySeparator(verPath),
			$"`{nameof(DataVersionFile)}` shouldn't end in a directory separator");

		try {
			FileMode lockFileMode;
			if (mode != KokoroContextOpenMode.ReadWriteCreate) {
				lockFileMode = FileMode.Open;
			} else {
				Directory.CreateDirectory(path); // Ensure exists
				lockFileMode = FileMode.OpenOrCreate;
			}

			// Attempt to acquire a lock
			lockHandle = File.OpenHandle(lockPath, lockFileMode, FileAccess.Read, FileShare.None, FileOptions.None, preallocationSize: 0);

			string rollbackPath = $"{dataPath}{RollbackSuffix}";
			if (mode != KokoroContextOpenMode.ReadOnly) {
				// Perform recovery if previous process crashed
				if (!TryPendingRollback(rollbackPath, dataPath)) {
					// Didn't rollback. Perhaps this is the initial access so…
					Directory.CreateDirectory(dataPath); // Ensure exists

					// Ensure version file exists
					if (!File.Exists(verPath)) goto DataVersionZeroInit;
				}
			} else if (HasPendingRollback(rollbackPath)) {
				// Can't perform recovery in read-only mode
				throw Ex_RollbackPendingButReadOnly_NS();
			}

			// Parse the existing version file
			using (FileStream verStream = new(verPath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 0, FileOptions.SequentialScan)) {
				Span<byte> verBytes = stackalloc byte[DataVersionFileHeaderWithVersion_MaxBytes];
				int verBytesRead = verStream.Read(verBytes);
				if (verBytesRead == 0) {
					if (mode != KokoroContextOpenMode.ReadOnly) {
						goto DataVersionZeroInit;
					}
					throw Ex_UnexpectedHeaderFormat_InvDat(verPath);
				}

				verBytes = verBytes[..verBytesRead];
				var verEnc = IOUtils.GetEncoding(verBytes);

				Span<char> verChars = stackalloc char[verEnc.GetCharCount(verBytes)];
				verEnc.GetChars(verBytes, verChars);

				if (!verChars.StartsWith(DataVersionFileHeader))
					throw Ex_UnexpectedHeaderFormat_InvDat(verPath);

				var verNums = verChars[DataVersionFileHeader.Length..];
				int verNumsEnd = verNums.IndexOfAny('\r', '\n');
				if (verNumsEnd >= 0) verNums = verNums[..verNumsEnd];

				try {
					_Version = KokoroDataVersion.Parse(verNums);
				} catch (FormatException ex) {
					throw Ex_UnexpectedHeaderFormat_InvDat(verPath, ex);
				}

				if (_Version.Operable) {
					// May throw if the current data to be loaded is invalid
					ForceLoadOperables();
					// Even our migration mechanism assumes that if the version
					// is correct (and not just operable), the data state should
					// be valid. And so, throwing now should be okay.
				}

				goto DataVersionLoaded;
			}

		DataVersionZeroInit:
			Debug.Assert(mode != KokoroContextOpenMode.ReadOnly, $"Shouldn't be reached when read-only");
			{
				// Create a new version file draft
				string verDraft = $"{verPath}{DraftSuffix}";
				using (FileStream verDraftStream = new(verDraft, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 0, FileOptions.WriteThrough)) {
					verDraftStream.Write(DataVersionFileHeaderZero.ToUTF8Bytes(
						stackalloc byte[DataVersionFileHeaderZero.GetUTF8ByteCount()]));
				}

				// Throws if version file exists with read-only attribute set
				File.Move(verDraft, verPath, overwrite: true);

				_Version = KokoroDataVersion.Zero;
				Debug.Assert(!_Version.Operable, "Version zero shouldn't be operable");
			}

		DataVersionLoaded:
			; // Nothing (for now)

		} catch (Exception ex) {
#if DEBUG
			lockHandle?.DisposeSafely(ex);
			// ^- `DisposeSafely()` is unnecessary above, unless we're manually
			// incrementing/decrementing the `SafeFileHandle` reference counter
			// and not doing so properly.
#else
			lockHandle?.Dispose();
#endif
			// Check if the cause of the exception is a read-only attribute
			if (ex is UnauthorizedAccessException
				&& File.Exists(verPath)
				&& (File.GetAttributes(verPath) & FileAttributes.ReadOnly) != 0
			) {
				// Throw a more meaningful exception than the framework-default
				// `UnauthorizedAccessException` that only says 'access denied'
				throw Ex_ReadOnlyAttr_RO(ex);
			}
			throw;
		}
		_LockHandle = lockHandle; // Mark as fully constructed
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static NotSupportedException Ex_RollbackPendingButReadOnly_NS()
		=> new($"Rollback pending, but can't rollback while opened in read-only mode");

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static InvalidDataException Ex_UnexpectedHeaderFormat_InvDat(string verPath, Exception? cause = null)
		=> new($"Unexpected header format for `{DataDir}/{DataVersionFile}` file." +
			$"{Environment.NewLine}Location: {verPath}", cause);

	#endregion

	#region Common Exceptions

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static ReadOnlyException Ex_ReadOnlyAttr_RO(Exception? cause = null)
		=> new("Read-only file attribute set, denying access", cause);

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static NotSupportedException Ex_ReadOnly_NS() => new($"Read-only");

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static NotSupportedException Ex_VersionNotOperable_NS(Exception? cause = null)
		=> new($"Version is not operable. Please migrate to the current operable vesrion first.", cause);

	#endregion

	// --

	#region Operable Resources

	#region Operable SQLite DB Access

	private string _OperableDbConnectionString = "";
	private DisposingObjectPool<KokoroSqliteDb>? _OperableDbPool;

	internal KokoroSqliteDb ObtainOperableDb() {
		DebugAssert_UsageMarked();

		KokoroSqliteDb? db;
		try {
			var pool = _OperableDbPool!;
			if (pool.TryTakeAggressively(out db)) {
				return db;
			}
			if (pool.IsDisposed) {
				throw Ex_ODisposed();
			}
		} catch (NullReferenceException ex) when (_OperableDbPool == null) {
			if (!_Version.Operable) throw Ex_VersionNotOperable_NS(ex);
			throw;
		}

		db = new(_OperableDbConnectionString);
		db.Open();
		return db;
	}

	internal void RecycleOperableDb(KokoroSqliteDb db) {
		DebugAssert_UsageMarked();

		try {
			_OperableDbPool!.TryPool(db);
		} catch (NullReferenceException ex) when (_OperableDbPool == null) {
			db.DisposeSafely(ex);
			if (!_Version.Operable) throw Ex_VersionNotOperable_NS(ex);
			throw;
		}
	}

	#endregion

	internal void LoadOperables() {
		DebugAssert_UsageMarkedExclusive();
		// Load only when not already loaded
		if (_OperableDbPool == null) {
			DebugAssert_VersionOperable();
			ForceLoadOperables();
		}
	}

	internal void UnloadOperables() {
		DebugAssert_UsageMarkedExclusive();
		// Unload only when not already unloaded
		if (_OperableDbPool != null) {
			DebugAssert_VersionOperable();
			ForceUnloadOperables();
		}
	}

	internal void ForceLoadOperables() {
		DebugAssert_UsageMarkedExclusive_Or_GuaranteedExclusiveUse();
		Debug.Assert(_OperableDbPool == null, "Operables already loaded");

		SqliteConnectionStringBuilder connStrBuilder = new() {
			Pooling = false, // We do our own pooling
			RecursiveTriggers = true,
		};

		string colDbPath = Path.Join(DataPath, "col.db", "main");
		if (!IsReadOnly) {
			connStrBuilder.DataSource = colDbPath;
			connStrBuilder.Mode = SqliteOpenMode.ReadWrite;
		} else {
			connStrBuilder.DataSource = $"{SqliteUtils.ToUriFilename(colDbPath)}?immutable=1";
			connStrBuilder.Mode = SqliteOpenMode.ReadOnly;
		}
		string connStr = connStrBuilder.ToString(); // Maybe throws (just maybe)
		KokoroSqliteDb db = new(connStr);
		db.Open();

		LoadNextRowIdsFrom(db);

		DisposingObjectPool<KokoroSqliteDb> pool = new();
		pool.TryPool(db);

		_OperableDbConnectionString = connStr;
		_OperableDbPool = pool; // Finally, mark as loaded
	}

	internal void ForceUnloadOperables() {
		DebugAssert_UsageMarkedExclusive_Or_GuaranteedExclusiveUse();
		Debug.Assert(_OperableDbPool != null, "Operables already unloaded");
		// TODO Assert that all DB connections have already been disposed

		_OperableDbPool!.Dispose(); // May throw

		_OperableDbConnectionString = "";
		_OperableDbPool = null; // Finally, mark as unloaded
	}

	internal void DisposeOperables() {
		DebugAssert_UsageMarkedExclusivelyForDispose();
		// Unload only when not already unloaded
		if (_OperableDbPool != null) {
			ForceUnloadOperables();
		}
	}

	[Conditional("DEBUG")]
	private void DebugAssert_VersionOperable()
		=> Debug.Assert(_Version.Operable, "Version should be operable");

	#endregion

	#region Atomic Data Restructuring

#if DEBUG
	private bool _DEBUG_PendingDataDirTransaction;
#else
	private const bool _DEBUG_PendingDataDirTransaction = false; // Dummy for `!DEBUG`
#endif

	// TODO Remove `DataDirTransaction`. Have methods instead direcly under
	// `KokoroContext` that handle data restructuring, with automatic disposal
	// of the context object itself when rollback fails.
	internal ref struct DataDirTransaction {
		private string? _DataRollbackPath; // Also used to indicate completion when null
		private readonly string _DataPath;
#if DEBUG
		private readonly KokoroContext _Context;
#endif

		public DataDirTransaction(KokoroContext context) {
			Debug.Assert(!context.IsReadOnly, $"Shouldn't be used when read-only");
			Debug.Assert(context.UsageMarkedExclusive_NV, "Shouldn't be used without first marking exclusive usage");
#if DEBUG
			Debug.Assert(!context._DEBUG_PendingDataDirTransaction, $"Only one `{nameof(DataDirTransaction)}` should be in use");
#endif

			string dataPath = context.DataPath;
			string rollbackPath = $"{dataPath}{RollbackSuffix}";

			// Ensure an empty rollback draft directory
			string rollbackDraftPath = $"{rollbackPath}{DraftSuffix}";
			if (Directory.Exists(rollbackDraftPath)) {
				// Clear the old rollback draft directory
				// -- possibly existing due to a previous process crash
				FsUtils.ClearDirectory(rollbackDraftPath);
			} else {
				Directory.CreateDirectory(rollbackDraftPath);
			}

			Debug.Assert(File.Exists(Path.Join(dataPath, DataVersionFile))
				, $"Unexpected: `{nameof(KokoroContext)}` should've created `{DataDir}/{DataVersionFile}`");

			// Backup data directory contents to the rollback draft
			FsUtils.CopyDirContents(dataPath, rollbackDraftPath);
			// TODO-XXX For each file being copied above, acquire an exclusive
			// lock for writing, and only after all needed files have been
			// copied should the acquired locks be released. If a lock can't be
			// acquired, throw.
			//
			// This is to ensure that no other process is modifying the files
			// being copied. Not doing so could cause files to go out of sync,
			// and some software being used (either by us or the user) might see
			// unexpected mispairings. For example, SQLite currently operates
			// across multiple files (e.g., the database file and its journal).
			//
			// Now, to ensure that anti-virus software doesn't cause us to
			// throw, perhaps we only need to prevent other processes from
			// writing to the files we're about to copy, i.e., perhaps use at
			// least `FileShare.Read` (instead of `FileShare.None`). See also,
			// https://stackoverflow.com/q/876473
			//
			// --
			// UPDATE: There's a hard limit to the number of files that can be
			// opened simultaneously. See, https://stackoverflow.com/q/20289565
			//
			// Perhaps simply use a file system watcher, and try to detect for
			// any changes to the underlying directory being copied. If after
			// the copy operation, we found out that the file system watcher
			// detected some changes, we abort the transaction creation.

			try {
				// Atomically mark the rollback draft as ready
				Directory.Move(rollbackDraftPath, rollbackPath);
			} catch (IOException) {
				// Check if there's an existing rollback directory that caused
				// the error, and if so, delete it instead, but only if our
				// rollback draft still exists.
				if (!Directory.Exists(rollbackPath) || !Directory.Exists(rollbackDraftPath)) {
					throw;
				}

				// Atomically delete the old rollback directory
				FsUtils.DeleteDirectoryAtomic(rollbackPath, context.EnsureTrash());

				// Atomically mark the rollback draft as ready (again)
				Directory.Move(rollbackDraftPath, rollbackPath);
			}

#if DEBUG
			context._DEBUG_PendingDataDirTransaction = true;
			_Context = context;
#endif
			_DataPath = dataPath;
			_DataRollbackPath = rollbackPath; // Mark as disposable
		}

		public readonly bool IsComplete => _DataRollbackPath == null;

		public void MarkComplete() {
			_DataRollbackPath = null; // Mark as no longer disposable
#if DEBUG
			_Context._DEBUG_PendingDataDirTransaction = false;
#endif
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static InvalidOperationException Ex_AlreadyComplete()
			=> new("This transaction has already completed; it is no longer usable.");

		// --

		public void Dispose() {
			if (_DataRollbackPath is string rollbackPath) {
				TryPendingRollback(rollbackPath, _DataPath);
				MarkComplete(); // Done!
			}
		}

		public bool TryRollback() {
			if (_DataRollbackPath is string rollbackPath) {
				if (TryPendingRollback(rollbackPath, _DataPath)) {
					MarkComplete();
					return true; // Done!
				}

				// Treat an invalid/missing rollback directory as an indication
				// of a prior commit/rollback, even though someone else might
				// have altered it instead to make it seem invalid.
				MarkComplete();
			}
			return false;
		}

		public void Rollback() {
			if (_DataRollbackPath is string rollbackPath) {
				if (TryPendingRollback(rollbackPath, _DataPath)) {
					MarkComplete();
					return; // Done!
				}

				// Treat an invalid/missing rollback directory as an indication
				// of a prior commit/rollback, even though someone else might
				// have altered it instead to make it seem invalid.
				MarkComplete();

				// Throw an appropriate exception
				if (Directory.Exists(rollbackPath)) {
					// The rollback directory exists but is invalid
					throw Rollback__E_RollbackVerMissing(rollbackPath);
				}
			}
			throw Rollback__E_AlreadyComplete();
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		[DoesNotReturn]
		private static Exception Rollback__E_AlreadyComplete() => throw Ex_AlreadyComplete();

		[MethodImpl(MethodImplOptions.NoInlining)]
		[DoesNotReturn]
		private static Exception Rollback__E_RollbackVerMissing(string rollbackPath) {
			string rollbackVerPath = Path.Join(rollbackPath, DataVersionFile);
			Debug.Assert(!File.Exists(rollbackVerPath), $"Expected not to exist at this point: {rollbackVerPath}");

			throw new InvalidOperationException(
				$"Invalid rollback directory. Expected file is missing: `{DataRollbackDir}/{DataVersionFile}`{Environment.NewLine}" +
				$"Expected location: {rollbackVerPath}");
		}

		// --

		public bool TryCommit() {
			if (_DataRollbackPath is not string rollbackPath) {
				return false;
			}

			// Delete the old, stale rollback directory
			// -- possibly existing due to a previous process crash
			string stalePath = $"{rollbackPath}{StaleSuffix}";
			if (Directory.Exists(stalePath)) {
				FsUtils.DeleteDirectory(stalePath);
			}

			// Now that the old, stale rollback directory is gone…
			try {
				// Atomically mark the rollback as stale (preventing rollback)
				Directory.Move(rollbackPath, stalePath);
			} catch (DirectoryNotFoundException) {
				if (!Directory.Exists(rollbackPath)) {
					// Treat the missing rollback directory as an indication of
					// a prior commit/rollback, even though someone else might
					// have removed it instead.
					MarkComplete(); // Still need to do this though
					return false;
				}
				throw; // It's some other exception
			}

			// Done! The rest are just cleanup (that can throw)
			MarkComplete();

			try {
				// Clean up the new, stale rollback directory
				FsUtils.DeleteDirectory(stalePath);
			} catch (Exception ex) {
				// Swallow
				Trace.WriteLine(ex);
				// ^ We shouldn't throw at this point. The act of removing the
				// rollback directory itself marks the commit as successful. If
				// we throw now, then the caller might assume that the commit
				// failed and must rollback any associated in-memory state, only
				// to be unable to rollback the on-disk representation itself
				// (as we've removed the rollback directory already).
			}
			return true;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Commit() {
			if (!TryCommit())
				throw Commit__E_AlreadyComplete();
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		[DoesNotReturn]
		private static Exception Commit__E_AlreadyComplete() => throw Ex_AlreadyComplete();
	}

	private static bool HasPendingRollback(string rollbackPath)
		=> File.Exists(Path.Join(rollbackPath, DataVersionFile));

	/// <remarks>
	/// Note: A successful rollback won't revert <see cref="_Version"/>. If the
	/// data version file changes as a result of rollback, <see cref="_Version"/>
	/// must be updated manually.
	/// </remarks>
	private static bool TryPendingRollback(string rollbackPath, string dataPath) {
		if (!HasPendingRollback(rollbackPath)) {
			return false; // Nothing to rollback
		}

		if (Directory.Exists(dataPath)) {
			FsUtils.DeleteDirectory(dataPath);
		}
		Directory.Move(rollbackPath, dataPath);

		return true;
	}

	#endregion

	#region Migration Mechanism

	public enum MigrationResultCode {
		// NOTE: The least-significant bit indicates equality
		Success                            = 0b00,
		FailedWithCurrentLessThanTarget    = 0b01,
		FailedWithCurrentEqualsTarget      = 0b10,
		FailedWithCurrentGreaterThanTarget = 0b11,
	}

	private const MigrationResultCode MigrationResultCode_CurrentNotEqualsTarget_Mask = (MigrationResultCode)0b01;

	public readonly record struct MigrationResult(MigrationResultCode ResultCode, KokoroDataVersion Current) {
		public bool Success => ResultCode == MigrationResultCode.Success;
		public bool EqualsTarget => (ResultCode & MigrationResultCode_CurrentNotEqualsTarget_Mask) == 0;

		public bool LessThanTarget => ResultCode == MigrationResultCode.FailedWithCurrentLessThanTarget;
		public bool LessThanOrEqualsTarget => !GreaterThanTarget;

		public bool GreaterThanTarget => ResultCode == MigrationResultCode.FailedWithCurrentGreaterThanTarget;
		public bool GreaterThanOrEqualsTarget => !LessThanTarget;
	}

	/// <returns>
	/// A <see cref="MigrationResult"/> indicating whether or not the upgrade
	/// attempt succeeded, along with the current <see cref="Version">version</see>
	/// after the attempt succeeded/failed.
	/// <para>
	/// Note that, if the current <see cref="Version">version</see> is already
	/// the same or higher than the specified <paramref name="target"/>, then
	/// no upgrade is attempted and the current version is returned in the
	/// <see cref="MigrationResult">result</see> as is.
	/// </para>
	/// </returns>
	/// <exception cref="NotSupportedException">
	/// <see cref="IsReadOnly"/> is <see langword="true"/>.
	/// </exception>
	/// <exception cref="NotImplementedException">
	/// Migration to the specified <paramref name="target"/> is not implemented.
	/// </exception>
	[SkipLocalsInit]
	public MigrationResult TryUpgradeToVersion(KokoroDataVersion target) {
		// TODO An overload that reports progress via file system watchers.
		// - Simply set up some file system watchers then have the new method
		// overload call onto this original overload.

		MarkUsageExclusive();
		try {
			var current = _Version;
			if (current >= target) {
				return new(current > target
					? MigrationResultCode.FailedWithCurrentGreaterThanTarget
					: MigrationResultCode.FailedWithCurrentEqualsTarget, current);
			}
			if (IsReadOnly) throw Ex_ReadOnly_NS();

			UnloadOperables();
			DataDirTransaction dataDirTransaction = new(this);
			try {
				var (actions, keys) = GetMigrations();
				do {
					int i = Array.BinarySearch(keys, (current, target));

					KokoroDataVersion to;
					if (i < 0) {
						i = ~i - 1;
						// ^ The index of the highest key strictly less than
						// the lookup key.

						if (i < 0) goto Fail_MigrationMissing;

						(var from, to) = keys[i];
						if (from == current && from < to) goto FoundIt;

						// Explanation:
						// - If `from != current`, there's no migration mapping
						// for the current version to the given target. Also
						// `from < current` is guaranteed (unless the keys are
						// unsorted or the lookup logic is incorrect).
						// - If `from > to`, it's a downgrade migration. And
						// we're expecting an upgrade operation, right?
						// - There shouldn't be a migration where `from == to`

						Debug.Assert(from != to, $"Unexpected migration mapping: {from} to {to}");
						Debug.Assert(from <= current, $"Unexpected state where `{nameof(from)} > {nameof(current)}` " +
							$"while migrating the current version {current} via the mapping '{from} to {to}'");

					Fail_MigrationMissing:
						throw Ex_ExpectedMigration_NI(current, target);

					} else {
						(_, to) = keys[i];
					}

				FoundIt:
					actions[i](this);
					current = to;

				} while (current < target);

				// Migration done! Now, wrap up everything.
				FinalizeVersionFileForMigration(current);

				// May throw if the current data to be loaded is invalid
				if (current.Operable) ForceLoadOperables();
				// ^ If our migration is properly set up, the above shouldn't
				// throw. Otherwise, the current migration mapping created an
				// unloadable, invalid data when it shouldn't; thus, we'll just
				// let our migration mechanism to catch the exception and
				// rollback accordingly.

				try {
					dataDirTransaction.Commit();
				} catch (Exception ex) {
					// Commit failed. Thus, undo the loading done earlier.
					try {
						if (current.Operable) ForceUnloadOperables();
					} catch (Exception ex2) {
						throw new DisposeAggregateException(ex, ex2);
					}
					throw;
				}
				_Version = current; // Success!

				return new(MigrationResultCode.Success, current);

			} catch (Exception ex) {
				try {
#if DEBUG
					dataDirTransaction.Rollback();
#else
					dataDirTransaction.Dispose();
#endif
				} catch (Exception ex2) {
#if DEBUG
					_DEBUG_PendingDataDirTransaction = false;
#endif
					// Unless we can rollback successfully, we're currently in
					// an unusable state. So let that state materialize then.
					Dispose();

					throw new DisposeAggregateException(ex, ex2);
				}
				throw; // Rollback successful so throw the original
			}
		} finally {
			UnMarkUsageExclusive();
		}
	}

	/// <returns>
	/// A <see cref="MigrationResult"/> indicating whether or not the downgrade
	/// attempt succeeded, along with the current <see cref="Version">version</see>
	/// after the attempt succeeded/failed.
	/// <para>
	/// Note that, if the current <see cref="Version">version</see> is already
	/// the same or lower than the specified <paramref name="target"/>, then no
	/// downgrade is attempted and the current version is returned in the
	/// <see cref="MigrationResult">result</see> as is.
	/// </para>
	/// </returns>
	/// <exception cref="NotSupportedException">
	/// <see cref="IsReadOnly"/> is <see langword="true"/>.
	/// </exception>
	/// <exception cref="NotImplementedException">
	/// Migration to the specified <paramref name="target"/> is not implemented.
	/// </exception>
	[SkipLocalsInit]
	public MigrationResult TryDowngradeToVersion(KokoroDataVersion target) {
		// TODO An overload that reports progress via file system watchers.
		// - Simply set up some file system watchers then have the new method
		// overload call onto this original overload.

		MarkUsageExclusive();
		try {
			var current = _Version;
			if (current <= target) {
				return new(current < target
					? MigrationResultCode.FailedWithCurrentLessThanTarget
					: MigrationResultCode.FailedWithCurrentEqualsTarget, current);
			}
			if (IsReadOnly) throw Ex_ReadOnly_NS();

			UnloadOperables();
			DataDirTransaction dataDirTransaction = new(this);
			try {
				var (actions, keys) = GetMigrations();
				int len = keys.Length;
				do {
					int i = Array.BinarySearch(keys, (current, target));

					KokoroDataVersion to;
					if (i < 0) {
						i = ~i;
						// ^ The index of the lowest key strictly greater than
						// the lookup key.

						if (i >= len) goto Fail_MigrationMissing;

						(var from, to) = keys[i];
						if (from == current && from > to) goto FoundIt;

						// Explanation:
						// - If `from != current`, there's no migration mapping
						// for the current version to the given target. Also
						// `from > current` is guaranteed (unless the keys are
						// unsorted or the lookup logic is incorrect).
						// - If `from < to`, it's an upgrade migration. And
						// we're expecting a downgrade operation, right?
						// - There shouldn't be a migration where `from == to`

						Debug.Assert(from != to, $"Unexpected migration mapping: {from} to {to}");
						Debug.Assert(from >= current, $"Unexpected state where `{nameof(from)} < {nameof(current)}` " +
							$"while migrating the current version {current} via the mapping '{from} to {to}'");

					Fail_MigrationMissing:
						throw Ex_ExpectedMigration_NI(current, target);

					} else {
						(_, to) = keys[i];
					}

				FoundIt:
					actions[i](this);
					current = to;

				} while (current > target);

				// Migration done! Now, wrap up everything.
				FinalizeVersionFileForMigration(current);

				// May throw if the current data to be loaded is invalid
				if (current.Operable) ForceLoadOperables();
				// ^ If our migration is properly set up, the above shouldn't
				// throw. Otherwise, the current migration mapping created an
				// unloadable, invalid data when it shouldn't; thus, we'll just
				// let our migration mechanism to catch the exception and
				// rollback accordingly.

				try {
					dataDirTransaction.Commit();
				} catch (Exception ex) {
					// Commit failed. Thus, undo the loading done earlier.
					try {
						if (current.Operable) ForceUnloadOperables();
					} catch (Exception ex2) {
						throw new DisposeAggregateException(ex, ex2);
					}
					throw;
				}
				_Version = current; // Success!

				return new(MigrationResultCode.Success, current);

			} catch (Exception ex) {
				try {
#if DEBUG
					dataDirTransaction.Rollback();
#else
					dataDirTransaction.Dispose();
#endif
				} catch (Exception ex2) {
#if DEBUG
					_DEBUG_PendingDataDirTransaction = false;
#endif
					// Unless we can rollback successfully, we're currently in
					// an unusable state. So let that state materialize then.
					Dispose();

					throw new DisposeAggregateException(ex, ex2);
				}
				throw; // Rollback successful so throw the original
			}
		} finally {
			UnMarkUsageExclusive();
		}
	}

	[SkipLocalsInit]
	private void FinalizeVersionFileForMigration(KokoroDataVersion newVersion) {
		Debug.Assert(Mode != KokoroContextOpenMode.ReadOnly, $"Shouldn't be called when read-only");

		string verPath = Path.Join(DataPath, DataVersionFile);
		using FileStream verStream = new(verPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 0, FileOptions.WriteThrough);

		string verStr = $"{DataVersionFileHeader}{newVersion}";
		verStream.Write(verStr.ToUTF8Bytes(stackalloc byte[verStr.GetUTF8ByteCount()]));
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static NotImplementedException Ex_ExpectedMigration_NI(KokoroDataVersion fromVersion, KokoroDataVersion toVersion)
		=> new($"Expected migration from {fromVersion} to {toVersion} is apparently not implemented");

	#endregion

	#region Usage Marking

	private uint _MarkUsageState;

	private const uint MarkUsageState_DisposedFlag  = 0b_001;
	private const uint MarkUsageState_DisposingFlag = 0b_010;
	private const uint MarkUsageState_ExclusiveFlag = 0b_100;

	private const  int MarkUsageState_SharedCountShift = 3;
	private const  int MarkUsageState_UsageMarkedShift = 2;

	private const  int MarkUsageState_DisposingOrUsageMarked_Shift = 1;

	private const uint MarkUsageState_SharedIncrement = 1 << MarkUsageState_SharedCountShift;
	private const uint MarkUsageState_SharedDecrement = unchecked((uint) -MarkUsageState_SharedIncrement);

	private const uint MarkUsageState_SharedForbiddenMask = ~MarkUsageState_SharedDecrement;

	private const uint MarkUsageState_Disposing =
		MarkUsageState_DisposedFlag | MarkUsageState_DisposingFlag;

	private const uint MarkUsageState_DisposedWhileExclusive =
		MarkUsageState_Disposing | MarkUsageState_ExclusiveFlag;

	private const uint MarkUsageState_DisposedWhileShared =
		MarkUsageState_Disposing + MarkUsageState_SharedIncrement;

	// --

	private uint MarkUsageState_Volatile => Volatile.Read(ref _MarkUsageState);

	public bool UsageMarked => (MarkUsageState_Volatile >> MarkUsageState_UsageMarkedShift) != 0;
	public bool UsageMarked_NV => (_MarkUsageState >> MarkUsageState_UsageMarkedShift) != 0;

	public bool UsageMarkedShared => (MarkUsageState_Volatile >> MarkUsageState_SharedCountShift) != 0;
	public bool UsageMarkedShared_NV => (_MarkUsageState >> MarkUsageState_SharedCountShift) != 0;

	public bool UsageMarkedExclusive => (MarkUsageState_Volatile & MarkUsageState_ExclusiveFlag) != 0;
	public bool UsageMarkedExclusive_NV => (_MarkUsageState & MarkUsageState_ExclusiveFlag) != 0;


	public bool IsDisposed => (MarkUsageState_Volatile & MarkUsageState_Disposing) != 0;
	public bool IsDisposed_NV => (_MarkUsageState & MarkUsageState_Disposing) != 0;

	private bool IsDisposing => MarkUsageState_Volatile == MarkUsageState_Disposing;
	private bool IsDisposing_NV => _MarkUsageState == MarkUsageState_Disposing;

	// --

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal void MarkUsageShared() {
		// A `SpinWait` would be overkill for this particular case.
		// - Think of `Interlocked.Increment()` but with a few extra conditions.
		// - In fact, `Interlocked.Or()` is implemented simply as a CAS loop
		// without a `SpinWait`.
		for (; ; ) {
			// A volatile read isn't needed here: we don't care if the load op
			// gets reordered to happen either earlier (by being preloaded in a
			// register) or later than expected (and if so, it won't get past
			// the `Interlocked` barrier anyway). Furthermore, a volatile read
			// doesn't even guarantee that we won't get a stale copy: it only
			// guarantees that 'later' loads won't fetch a stale copy (at least,
			// according to the current C# spec).
			uint state = _MarkUsageState;
			if ((state & MarkUsageState_SharedForbiddenMask) != 0) {
				throw MarkUsageShared__E_Fail_InvOp(state);
			}
			// Increment the shared usage count (while checking for overflows)
			if (Interlocked.CompareExchange(ref _MarkUsageState
				, checked(state + MarkUsageState_SharedIncrement)
				, state) == state) break;
		}
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[DoesNotReturn]
	private Exception MarkUsageShared__E_Fail_InvOp(uint lastMarkUsageState) {
		if ((lastMarkUsageState & MarkUsageState_Disposing) != 0) {
			throw Ex_ODisposed();
		}
		if ((lastMarkUsageState & MarkUsageState_ExclusiveFlag) != 0) {
			throw new InvalidOperationException($"Currently locked for exclusive use");
		}
		Trace.Fail("Unexpected exception path");
		throw new InvalidOperationException("NA");
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal void UnMarkUsageShared() {
		const string AssertFailedMessage_UnMarkUsageShared_Unbalanced =
			$"`{nameof(UnMarkUsageShared)}()` should only be called after a " +
			$"successful call to `{nameof(MarkUsageShared)}()`";

		// Assert that we won't decrement to a negative count (a volatile read isn't used to not disturb normal code)
		Debug.Assert((_MarkUsageState >> MarkUsageState_SharedCountShift) != 0
			, AssertFailedMessage_UnMarkUsageShared_Unbalanced);

		// Decrement the shared usage count (without checking for underflows)
		uint newState = Interlocked.Add(ref _MarkUsageState, MarkUsageState_SharedDecrement);

		// Assert that we didn't decrement to a negative count
		Debug.Assert((unchecked((int)newState) >> MarkUsageState_SharedCountShift) != -1
			, AssertFailedMessage_UnMarkUsageShared_Unbalanced);

		if (newState == MarkUsageState_Disposing) {
			const uint oldState = unchecked(MarkUsageState_Disposing - MarkUsageState_SharedDecrement);

			// Last usage unmarked after a previous `Dispose(bool)` call
			DisposingCore(oldState); // Dispose here then.

			return; // Skip code below
		}

		Debug.Assert((newState & MarkUsageState_SharedForbiddenMask) == 0
			, $"Unexpected flags after `{nameof(UnMarkUsageShared)}()`: 0b_{Convert.ToString(newState, 2)}");
	}


	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal void MarkUsageExclusive() {
		uint state = Interlocked.CompareExchange(ref _MarkUsageState, MarkUsageState_ExclusiveFlag, 0);
		if (state != 0) {
			throw MarkUsageExclusive__E_Fail_InvOp(state);
		}
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[DoesNotReturn]
	private Exception MarkUsageExclusive__E_Fail_InvOp(uint lastMarkUsageState) {
		if ((lastMarkUsageState & MarkUsageState_Disposing) != 0) {
			throw Ex_ODisposed();
		}
		if ((lastMarkUsageState & MarkUsageState_ExclusiveFlag) != 0) {
			throw new InvalidOperationException($"Already locked for exclusive use");
		}
		if ((lastMarkUsageState >> MarkUsageState_SharedCountShift) != 0) {
			throw new InvalidOperationException($"Couldn't lock for exclusive use, as it's already being used");
		}
		Trace.Fail("Unexpected exception path");
		throw new InvalidOperationException("NA");
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal void UnMarkUsageExclusive() {
		const string AssertFailedMessage_UnMarkUsageExclusive_Unbalanced =
			$"`{nameof(UnMarkUsageExclusive)}()` should only be called after a " +
			$"successful call to `{nameof(MarkUsageExclusive)}()`";

		Debug.Assert(!_DEBUG_PendingDataDirTransaction, $"Should not unmark exclusive " +
			$"usage while there's a pending `{nameof(DataDirTransaction)}`");

		uint oldState = Interlocked.CompareExchange(ref _MarkUsageState, 0, MarkUsageState_ExclusiveFlag);
		if (oldState == MarkUsageState_ExclusiveFlag) return; // Success

		// Assume a pending dispose request and "no other possibility"
		Debug.Assert(oldState == MarkUsageState_DisposedWhileExclusive
			, AssertFailedMessage_UnMarkUsageExclusive_Unbalanced);

		// Lazily unset the exclusive flag while preserving the dispose request
		_MarkUsageState = MarkUsageState_Disposing;
		// ^ A volatile write isn't needed here: we don't care if the store op
		// gets reordered to happen either earlier (it won't get past the
		// `Interlocked` barrier anyway) or later than expected. It will
		// eventually be visible to other threads anyway. Moreover, no one can
		// mark for further usage now, as we're ALREADY FLAGGED as DISPOSED.

		// Last usage unmarked. Dispose here then.
		DisposingCore(oldState);
	}

	#region Debugging Convenience & Utilities

	[Conditional("DEBUG")]
	private void DebugAssert_UsageMarked()
		=> Debug.Assert(UsageMarked_NV, "Shouldn't be called without first marking usage");

	[Conditional("DEBUG")]
	private void DebugAssert_UsageMarkedExclusive()
		=> Debug.Assert(UsageMarkedExclusive_NV, "Shouldn't be called without first marking exclusive usage");

	[Conditional("DEBUG")]
	private void DebugAssert_UsageMarkedExclusive_Or_GuaranteedExclusiveUse()
		=> Debug.Assert(UsageMarkedExclusive_NV || !IsConstructorDone || IsDisposing_NV,
			"Shouldn't be called without first marking exclusive usage");

	[Conditional("DEBUG")]
	private void DebugAssert_UsageMarkedExclusivelyForDispose()
		=> Debug.Assert(IsDisposing_NV, $"Shouldn't be called {(UsageMarked_NV
			? "while usage is marked" : "when not marked exclusively for disposal")}");

	#endregion

	#endregion

	#region `IDisposable` implementation

	private void DisposingCore(uint lastMarkUsageState) {
		DebugAssert_UsageMarkedExclusivelyForDispose();

		// Dispose managed state (managed objects).
		//
		// NOTE: If we're here, then we're sure that the constructor completed
		// successfully. Fields that aren't supposed to be null are guaranteed
		// to be non-null.
		// --

		try {
			DisposeOperables(); // Allowed to throw

			// Should be done last, as it'll release the lock
			_LockHandle?.Dispose();
			// ^- Also, we shouldn't really release the lock while everything is
			// still not yet completely disposed (due to an exception).
			// --
		} catch (Exception ex) when (
			// Disposal is considered complete the moment the lock handle is
			// released and closed. Therefore, don't proceed below if disposal
			// did succeed (as the code after may undo the disposing flag).
			//
			// Normally, repeated calls to `SafeFileHandle.Dispose()` shouldn't
			// throw, but that is only when `SafeFileHandle.DangerousRelease()`
			// isn't used to dispose the file handle beforehand.
			!_LockHandle.IsClosed
		) {
			Debug.Assert(IsDisposing_NV,
				$"Should still be marked exclusively for disposal at this point.{Environment.NewLine}" +
				$"Other exception:{Environment.NewLine}{ex}");

			Debug.Assert((lastMarkUsageState & MarkUsageState_DisposedFlag) != 0,
				$"State to revert into should be flagged as disposed (as we're" +
				$" already partially disposed).{Environment.NewLine}" +
				$"Other exception:{Environment.NewLine}{ex}");

			// Revert into a state prior disposal that's expected to allow
			// disposal to be restarted.
			Volatile.Write(ref _MarkUsageState, lastMarkUsageState);
			// ^ NOTE: If we're disposing, then no one else can change the usage
			// state now, as we're ALREADY FLAGGED as DISPOSED.

			throw;
		}
	}

	protected virtual void Dispose(bool disposing) {
		uint oldState = Interlocked.Or(ref _MarkUsageState, MarkUsageState_Disposing);
		if (disposing) {
			if ((oldState >> MarkUsageState_DisposingOrUsageMarked_Shift) == 0) {
				// No current usage or ongoing disposal op. Dispose here then.
				DisposingCore(lastMarkUsageState: MarkUsageState_DisposedFlag);
				// ^ NOTE: If disposal fails, we cannot just revert back to the
				// old state. Instead, we must revert into a state where we're
				// still FLAGGED as DISPOSED.
				Debug.Assert(oldState is 0 or MarkUsageState_DisposedFlag,
					$"Expected to simply revert into `{nameof(MarkUsageState_DisposedFlag)}`" +
					$" without needing to bitwise-OR the old state:{Environment.NewLine}" +
					$"    `{nameof(oldState)} == 0b_{Convert.ToString(oldState, 2)}`");
			} else {
				// Otherwise, the last usage will handle disposal.
			}
		}
	}

	~KokoroContext() => Dispose(disposing: false);

	public void Dispose() {
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
		// ^- Side-effect: `this` is kept alive 'til the method ends.
		// - See, https://stackoverflow.com/q/816818
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[DoesNotReturn]
	private void E_ODisposed() => throw Ex_ODisposed();

	[MethodImpl(MethodImplOptions.NoInlining)]
	private ObjectDisposedException Ex_ODisposed() => DisposeUtils.Ode(GetType());

	#endregion
}
