namespace Kokoro;
using Kokoro.DataProtocols;

partial class KokoroContext {

	private static partial void InitMigrationMap(MigrationMap map) {
		map.Add(Setup_v0w1_UpgradeFrom_v0w0.DoMigrate);
		map.Add(Setup_v0w1_DowngradeTo_v0w0.DoMigrate);
	}
}
