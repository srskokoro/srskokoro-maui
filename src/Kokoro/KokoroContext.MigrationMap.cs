namespace Kokoro;
using System.Text.RegularExpressions;

partial class KokoroContext {

	private static partial void InitMigrationMap(MigrationMap map);

	private static class MigrationMapCompiled {
		internal static readonly (KokoroDataVersion From, KokoroDataVersion To)[] VersionMappings;
		internal static readonly Action<KokoroContext>[] MigrationActions;

		static MigrationMapCompiled() {
			MigrationMap map = new();
			InitMigrationMap(map);
			VersionMappings = map.Keys.ToArray();
			MigrationActions = map.Values.ToArray();
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[SkipLocalsInit]
#if TEST
	private protected virtual
#else
	private static
#endif
	(Action<KokoroContext>[] MigrationActions, (KokoroDataVersion From, KokoroDataVersion To)[] VersionMappings)
		GetMigrations() => (MigrationMapCompiled.MigrationActions, MigrationMapCompiled.VersionMappings);

#if TEST
	internal
#else
	private
#endif
	sealed class MigrationMap : SortedList<(KokoroDataVersion From, KokoroDataVersion To), Action<KokoroContext>> {

		public void Add(Action<KokoroContext> migrationAction, (KokoroDataVersion From, KokoroDataVersion To) versionMapping) => Add(versionMapping, migrationAction);

		public void Add(Action<KokoroContext> migrationAction, [CallerArgumentExpression("migrationAction")] string migrationActionExprStr = "") {
			Match match = ConformingMigrationActionExprArg.Match(migrationActionExprStr);
			if (!match.Success) {
				Debug.Fail(
					$"Couldn't automatically deduce the version mapping for the given `{nameof(migrationAction)}` argument:" +
					$"{Environment.NewLine}   {migrationActionExprStr}" +
					$"{Environment.NewLine}" +
					$"{Environment.NewLine}Expected regex to match against:" +
					$"{Environment.NewLine}   {ConformingMigrationActionExprArg}" +
					$"{Environment.NewLine}___"
				);
				return; // Early exit
			}

			GroupCollection groups = match.Groups;

			KokoroDataVersion x = new(
				major: groups[1].ValueSpan,
				minor: groups[2].ValueSpan);

			KokoroDataVersion y = new(
				major: groups[4].ValueSpan,
				minor: groups[5].ValueSpan);

			(KokoroDataVersion Current, KokoroDataVersion Target) vers;

			ReadOnlySpan<char> actionSeq = groups[3].ValueSpan;
			if (actionSeq.SequenceEqual(/* x */"UpgradeFrom"/* y */)) {
				vers = (Current: y, Target: x);
			} else {
				Debug.Assert(actionSeq.SequenceEqual(/* x */"DowngradeTo"/* y */));
				vers = (Current: x, Target: y);
			}

			Debug.Assert(x > y,
				$"Unexpected '{actionSeq}' action bearing incorrect version mapping:" +
				$"{Environment.NewLine}   {migrationActionExprStr}");

			Add(vers, migrationAction);
		}

		private static readonly Regex ConformingMigrationActionExprArg = new(ConformingMigrationActionExprArg_Pattern, RegexOptions.Compiled);
		private const string ConformingMigrationActionExprArg_Pattern = $@"^Setup_v(\d+)w(\d+)_(UpgradeFrom|DowngradeTo)_v(\d+)w(\d+)\.";
	}
}
