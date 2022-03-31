namespace Kokoro;
using Kokoro.Common.IO;
using Kokoro.Common.Pooling;
using Kokoro.Internal.Sqlite;
using Microsoft.Win32.SafeHandles;

public partial class KokoroContext : IDisposable {

	#region Constants

	#region Forward-compatible Constants

	private const string LockFile = ".kokoro.lock";

	private const string RollbackSuffix = $".rollback";
	private const string DraftSuffix = $"-draft";
	private const string StaleSuffix = $"-stale";

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

	private const string TrashDir = $"trash";

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

			if (mode != KokoroContextOpenMode.ReadOnly) {
				// Perform recovery if previous process crashed
				if (!TryPendingRollback($"{dataPath}{RollbackSuffix}", dataPath)) {
					// Didn't rollback. Perhaps this is the initial access so…
					Directory.CreateDirectory(dataPath); // Ensure exists

					// Ensure version file exists
					if (!File.Exists(verPath)) goto DataVersionZeroInit;
				}
			}

			// Parse the existing version file
			using (FileStream verStream = new(verPath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 0, FileOptions.SequentialScan)) {
				Span<byte> verBytes = stackalloc byte[DataVersionFileHeaderWithVersion_MaxBytes];
				int verBytesRead = verStream.Read(verBytes);
				if (verBytesRead == 0) {
					if (mode != KokoroContextOpenMode.ReadOnly) {
						goto DataVersionZeroInit;
					}
					E_UnexpectedHeaderFormat_InvDat(verPath);
				}

				verBytes = verBytes[..verBytesRead];
				var verEnc = IOUtils.GetEncoding(verBytes);

				Span<char> verChars = stackalloc char[verEnc.GetCharCount(verBytes)];
				verEnc.GetChars(verBytes, verChars);

				if (!verChars.StartsWith(DataVersionFileHeader))
					E_UnexpectedHeaderFormat_InvDat(verPath);

				var verNums = verChars[DataVersionFileHeader.Length..];
				int verNumsEnd = verNums.IndexOfAny('\r', '\n');
				if (verNumsEnd >= 0) verNums = verNums[..verNumsEnd];

				try {
					_Version = KokoroDataVersion.Parse(verNums);
				} catch (FormatException ex) {
					E_UnexpectedHeaderFormat_InvDat(verPath, ex);
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
			}

		DataVersionLoaded:
			; // Nothing (for now)

		} catch (Exception ex) {
			// TODO `DisposeSafely()` is unnecessary here
			lockHandle?.DisposeSafely(ex);
			// Check if the cause of the exception is a read-only attribute
			if (ex is UnauthorizedAccessException
				&& File.Exists(verPath)
				&& (File.GetAttributes(verPath) & FileAttributes.ReadOnly) != 0
			) {
				// Throw a more meaningful exception than the framework-default
				// `UnauthorizedAccessException` that only says 'access denied'
				E_ReadOnlyAttr_RO(ex);
			}
			throw;
		}
		_LockHandle = lockHandle;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[DoesNotReturn]
	private static void E_UnexpectedHeaderFormat_InvDat(string verPath, Exception? cause = null) {
		throw new InvalidDataException(
			$"Unexpected header format for `{DataDir}/{DataVersionFile}` file." +
			$"{Environment.NewLine}Location: {verPath}", cause);
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[DoesNotReturn]
	private static void E_ReadOnlyAttr_RO(Exception? cause = null)
		=> throw new ReadOnlyException("Read-only file attribute set, denying access", cause);

	[MethodImpl(MethodImplOptions.NoInlining)]
	[DoesNotReturn]
	private static void E_ReadOnly_NS()
		=> throw new NotSupportedException($"Read-only");

	[MethodImpl(MethodImplOptions.NoInlining)]
	[DoesNotReturn]
	private static void E_VersionNotOperable_NS()
		=> throw new NotSupportedException($"Version is not operable. Please migrate to the current operable vesrion first.");

	// --

	#region Operable SQLite DB Access

	// TODO Fill on demand while honoring read-only mode
	private string? _OperableDbConnectionString;

	private DisposingObjectPool<KokoroSqliteDb>? _OperableDbPool;

	internal KokoroSqliteDb ObtainOperableDb() {
		Debug.Assert(UsageMarked_NV, "Shouldn't be called without first marking usage");
		if (!Version.Operable) E_VersionNotOperable_NS();
		if (!(_OperableDbPool!.TryTakeAggressively(out var db))) {
			db = new(_OperableDbConnectionString);
			db.Open();
		}
		return db;
	}

	internal void RecycleOperableDb(KokoroSqliteDb db) {
		Debug.Assert(UsageMarked_NV, "Shouldn't be called without first marking usage");
		Debug.Assert(Version.Operable, "Version should be operable");
		_OperableDbPool!.TryPool(db);
	}

	#endregion

	#region Atomic Data Restructuring

#if DEBUG
	private bool _DEBUG_PendingDataDirTransaction;
#else
	private const bool _DEBUG_PendingDataDirTransaction = false; // Dummy for `!DEBUG`
#endif

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
			_DataRollbackPath = null;
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
					Rollback__E_RollbackVerMissing(rollbackPath);
				}
			}
			Rollback__E_AlreadyComplete();
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		[DoesNotReturn]
		private static void Rollback__E_AlreadyComplete() => throw Ex_AlreadyComplete();

		[MethodImpl(MethodImplOptions.NoInlining)]
		[DoesNotReturn]
		private static void Rollback__E_RollbackVerMissing(string rollbackPath) {
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
				// Atomically mark the rollback as stale
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

			// Clean up the new, stale rollback directory
			FsUtils.DeleteDirectory(stalePath);
			return true;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Commit() {
			if (!TryCommit())
				Commit__E_AlreadyComplete();
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		[DoesNotReturn]
		private static void Commit__E_AlreadyComplete() => throw Ex_AlreadyComplete();
	}

	private static bool TryPendingRollback(string rollbackPath, string dataPath) {
		if (!File.Exists(Path.Join(rollbackPath, DataVersionFile))) {
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
		MarkUsageExclusive();
		try {
			var current = Version;
			if (current >= target) {
				return new(current > target
					? MigrationResultCode.FailedWithCurrentGreaterThanTarget
					: MigrationResultCode.FailedWithCurrentEqualsTarget, current);
			}
			if (IsReadOnly) E_ReadOnly_NS();

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

				// Done!
				FinalizeVersionFileForMigration(current);
				dataDirTransaction.Commit();

				return new(MigrationResultCode.Success, current);

			} catch (Exception ex) {
				try {
#if DEBUG
					dataDirTransaction.Rollback();
#else
					dataDirTransaction.Dispose();
#endif
				} catch (Exception ex2) {
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
		MarkUsageExclusive();
		try {
			var current = Version;
			if (current <= target) {
				return new(current < target
					? MigrationResultCode.FailedWithCurrentLessThanTarget
					: MigrationResultCode.FailedWithCurrentEqualsTarget, current);
			}
			if (IsReadOnly) E_ReadOnly_NS();

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

				// Done!
				FinalizeVersionFileForMigration(current);
				dataDirTransaction.Commit();

				return new(MigrationResultCode.Success, current);

			} catch (Exception ex) {
				try {
#if DEBUG
					dataDirTransaction.Rollback();
#else
					dataDirTransaction.Dispose();
#endif
				} catch (Exception ex2) {
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

		_Version = newVersion;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static NotImplementedException Ex_ExpectedMigration_NI(KokoroDataVersion fromVersion, KokoroDataVersion toVersion)
		=> new($"Expected migration from {fromVersion} to {toVersion} is apparently not implemented");

	#endregion

	#region Usage Marking

	private uint _MarkUsageState;

	private const uint MarkUsageState_DisposingFlag   = 0b_001;
	private const uint MarkUsageState_ExclusiveFlag   = 0b_010;
	private const uint MarkUsageState_SharedIncrement = 0b_100;
	private const uint MarkUsageState_SharedDecrement =
		unchecked((uint) -MarkUsageState_SharedIncrement);

	private const uint MarkUsageState_DisposingWhileExclusive =
		MarkUsageState_DisposingFlag | MarkUsageState_ExclusiveFlag;

	private const uint MarkUsageState_SharedForbiddenMask =
		MarkUsageState_DisposingFlag | MarkUsageState_ExclusiveFlag;

	private const uint MarkUsageState_SharedCount_Mask = MarkUsageState_SharedDecrement;

	private const uint MarkUsageState_UsageMarkedMask =
		MarkUsageState_ExclusiveFlag | MarkUsageState_SharedCount_Mask;

	// --

	private uint MarkUsageState_Volatile => Volatile.Read(ref _MarkUsageState);

	public bool UsageMarked => (MarkUsageState_Volatile & MarkUsageState_UsageMarkedMask) != 0;
	public bool UsageMarked_NV => (_MarkUsageState & MarkUsageState_UsageMarkedMask) != 0;

	public bool UsageMarkedShared => (MarkUsageState_Volatile & MarkUsageState_SharedCount_Mask) != 0;
	public bool UsageMarkedShared_NV => (_MarkUsageState & MarkUsageState_SharedCount_Mask) != 0;

	public bool UsageMarkedExclusive => (MarkUsageState_Volatile & MarkUsageState_ExclusiveFlag) != 0;
	public bool UsageMarkedExclusive_NV => (_MarkUsageState & MarkUsageState_ExclusiveFlag) != 0;

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
				MarkUsageShared__E_Fail_InvOp(state);
			}
			// Increment the shared usage count (while checking for overflows)
			if (Interlocked.CompareExchange(ref _MarkUsageState
				, checked(state + MarkUsageState_SharedIncrement)
				, state) == state) break;
		}
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[DoesNotReturn]
	private void MarkUsageShared__E_Fail_InvOp(uint lastMarkUsageState) {
		if ((lastMarkUsageState & MarkUsageState_DisposingFlag) != 0) {
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
		Debug.Assert((_MarkUsageState & MarkUsageState_SharedCount_Mask) != 0
			, AssertFailedMessage_UnMarkUsageShared_Unbalanced);

		// Decrement the shared usage count (without checking for underflows)
		uint newState = Interlocked.Add(ref _MarkUsageState, MarkUsageState_SharedDecrement);

		// Assert that we didn't decrement to a negative count
		Debug.Assert((newState & MarkUsageState_SharedCount_Mask) != MarkUsageState_SharedDecrement
			, AssertFailedMessage_UnMarkUsageShared_Unbalanced);

		if (newState == MarkUsageState_DisposingFlag) {
			// Last usage unmarked after a previous `Dispose(bool)` call
			DisposingCore(); // Dispose here then.
		}
	}


	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal void MarkUsageExclusive() {
		uint state = Interlocked.CompareExchange(ref _MarkUsageState, MarkUsageState_ExclusiveFlag, 0);
		if (state != 0) {
			MarkUsageExclusive__E_Fail_InvOp(state);
		}
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[DoesNotReturn]
	private void MarkUsageExclusive__E_Fail_InvOp(uint lastMarkUsageState) {
		if ((lastMarkUsageState & MarkUsageState_DisposingFlag) != 0) {
			throw Ex_ODisposed();
		}
		if ((lastMarkUsageState & MarkUsageState_ExclusiveFlag) != 0) {
			throw new InvalidOperationException($"Already locked for exclusive use");
		}
		if ((lastMarkUsageState & MarkUsageState_SharedCount_Mask) != 0) {
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
		Debug.Assert(oldState == MarkUsageState_DisposingWhileExclusive
			, AssertFailedMessage_UnMarkUsageExclusive_Unbalanced);

		// Lazily unset the exclusive flag while preserving the dispose request
		_MarkUsageState = MarkUsageState_DisposingFlag;
		// ^ A volatile write isn't needed here: we don't care if the store op
		// gets reordered to happen either earlier (it won't get past the
		// `Interlocked` barrier anyway) or later than expected. It will
		// eventually be visible to other threads anyway. Furthermore, no one
		// can change the current state now, as we're ALREADY DISPOSING.

		// Last usage unmarked. Dispose here then.
		DisposingCore();
	}

	#endregion

	#region `IDisposable` implementation

	private void DisposingCore() {
		// Dispose managed state (managed objects).
		//
		// NOTE: If we're here, then we're sure that the constructor completed
		// successfully. Fields that aren't supposed to be null are guaranteed
		// to be non-null.
		// --
		ICollection<Exception>? exc = null;
		{
			_OperableDbPool?.DisposeSafely(ref exc);

			// Should be done last (as it'll release the lock)
			_LockHandle.DisposeSafely(ref exc);
			// TODO ^- `DisposeSafely()` is unnecessary above
		}
		exc?.ReThrowFlatten();
	}

	protected virtual void Dispose(bool disposing) {
		uint oldState = Interlocked.Or(ref _MarkUsageState, MarkUsageState_DisposingFlag);
		if (disposing) {
			if (oldState == 0) {
				// No current usage. Dispose here then.
				DisposingCore();
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
