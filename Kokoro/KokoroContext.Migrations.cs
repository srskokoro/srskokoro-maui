using Microsoft.Data.Sqlite;

namespace Kokoro;

public partial class KokoroContext {

	private static partial SortedList<(int, int), Action<KokoroContext>> ProvideMigrationMap() => new() {
		{ (0, 1), _ => _.Upgrade_0_To_1() },
		{ (1, 0), _ => _.Downgrade_1_To_0() },
	};

	private void Upgrade_0_To_1() {
		// TODO
	}

	private void Downgrade_1_To_0() {
		// TODO
	}
}
