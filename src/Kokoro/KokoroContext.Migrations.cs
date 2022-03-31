namespace Kokoro;

partial class KokoroContext {

	private static partial void InitMigrationMap(MigrationMap map) {
		map.Add(_ => _.Upgrade_v0w0_To_v0w1());
		map.Add(_ => _.Downgrade_v0w1_To_v0w0());
	}

	private void Upgrade_v0w0_To_v0w1() => _ = this; // TODO

	private void Downgrade_v0w1_To_v0w0() => _ = this; // TODO
}
