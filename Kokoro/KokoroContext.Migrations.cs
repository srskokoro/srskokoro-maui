using Kokoro.Sqlite;
using Microsoft.Data.Sqlite;

namespace Kokoro;

partial class KokoroContext {

	private static partial SortedList<(int, int), Action<KokoroContext, KokoroSqliteDb>> ProvideMigrationMap() => new() {
		{ (0, 1), (_, db) => _.Upgrade_0_To_1(db) },
		{ (1, 0), (_, db) => _.Downgrade_1_To_0(db) },
	};

	private void Upgrade_0_To_1(KokoroSqliteDb db) {
		// TODO
	}

	private void Downgrade_1_To_0(KokoroSqliteDb db) {
		// TODO
	}
}
