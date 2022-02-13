using Kokoro.Util;

namespace Kokoro;

partial class KokoroContext {

	private readonly object _MigrationLock = new();

	public virtual void MigrateToVersion(int newVersion) {
		if (_Collection is not null)
			goto CollectionAlreadyExists;

		if (newVersion < 0)
			throw new ArgumentOutOfRangeException(nameof(newVersion), "New version cannot be less than zero.");

		if (newVersion > _MaxSupportedVersion)
			throw new ArgumentOutOfRangeException(nameof(newVersion), $"New version cannot be greater than `{nameof(MaxSupportedVersion)}` (currently {MaxSupportedVersion}).");

		lock (_MigrationLock) {
			if (_Collection is not null)
				goto CollectionAlreadyExists;

			using (var h = GetThreadDb(out var db)) {
				using var transaction = db.BeginTransaction();
				var oldVersion = db.Version;

				if (oldVersion != newVersion) {
					if (IsReadOnly)
						throw new NotSupportedException("Read-only");

					if (oldVersion < newVersion) {
						OnUpgrade(db, oldVersion, newVersion);
					} else {
						OnDowngrade(db, oldVersion, newVersion);
					}
					db.ExecuteNonQuery($"PRAGMA user_version = {newVersion}");

					transaction.Commit();
				}
			}
		}
		return; // --

	CollectionAlreadyExists:
		if (newVersion == KokoroCollection._OperableVersion) {
			return; // Already migrated and operable with collection already created
		}
		throw new InvalidOperationException($"Cannot migrate once `{nameof(Collection)}` property has already been accessed.");
	}

	public void MigrateToOperableVersion() => MigrateToVersion(KokoroCollection._OperableVersion);

	// --

	protected virtual void OnUpgrade(KokoroSqliteDb db, int oldVersion, int newVersion) {
		int curVersion = oldVersion;

		var actions = MigrationMap.Actions;
		var keys = MigrationMap.Keys;

		while (curVersion < newVersion) {
			int i = Array.BinarySearch(keys, (curVersion, newVersion));

			int target;
			if (i < 0) {
				i = ~i - 1;
				if (i < 0) {
					throw E_ExpectedMigration(curVersion, newVersion);
				}
				(int from, target) = keys[i];
				if (from != curVersion || from >= target) {
					throw E_ExpectedMigration(curVersion, newVersion);
				}
			} else {
				(_, target) = keys[i];
			}

			actions[i](this, db);
			curVersion = target;
		}
	}

	protected virtual void OnDowngrade(KokoroSqliteDb db, int oldVersion, int newVersion) {
		int curVersion = oldVersion;

		var actions = MigrationMap.Actions;
		var keys = MigrationMap.Keys;
		int len = keys.Length;

		while (curVersion > newVersion) {
			int i = Array.BinarySearch(keys, (curVersion, newVersion));

			int target;
			if (i < 0) {
				i = ~i;
				if (i >= len) {
					throw E_ExpectedMigration(curVersion, newVersion);
				}
				(int from, target) = keys[i];
				if (from != curVersion || from <= target) {
					throw E_ExpectedMigration(curVersion, newVersion);
				}
			} else {
				(_, target) = keys[i];
			}

			actions[i](this, db);
			curVersion = target;
		}
	}

	private static class MigrationMap {
		internal static readonly (int FromVersion, int ToVersion)[] Keys;
		internal static readonly Action<KokoroContext, KokoroSqliteDb>[] Actions;

		static MigrationMap() {
			var list = ProvideMigrationMap();
			Keys = list.Keys.ToArray();
			Actions = list.Values.ToArray();
		}
	}

	private static partial SortedList<(int, int), Action<KokoroContext, KokoroSqliteDb>> ProvideMigrationMap();

	private static NotImplementedException E_ExpectedMigration(int fromVersion, int toVersion)
		=> new($"Expected migration from {fromVersion} to {toVersion} is apparently not implemented.");
}
