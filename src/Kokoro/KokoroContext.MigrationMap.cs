namespace Kokoro;
using System.Text.RegularExpressions;

public partial class KokoroContext {

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
			const int VGroupOffset = ConformingMigrationActionExprArg_VersionGroupOffset;

			KokoroDataVersion from = new(
				major: groups[1 + VGroupOffset].ValueSpan,
				minor: groups[2 + VGroupOffset].ValueSpan);

			KokoroDataVersion to = new(
				major: groups[3 + VGroupOffset].ValueSpan,
				minor: groups[4 + VGroupOffset].ValueSpan);

			Debug.Assert(from != to, $"Unexpected migration mapping: {from} to {to}");
			Debug.Assert(from < to
				? groups[1].ValueSpan.SequenceEqual("Up")
				: groups[1].ValueSpan.SequenceEqual("Down"),
				$"Unexpected '{(from < to ? "up" : "down")}grade' action bearing an incorrect naming:" +
				$"{Environment.NewLine}   {migrationActionExprStr}");

			Add((from, to), migrationAction);
		}

		private static readonly Regex ConformingMigrationActionExprArg = new(ConformingMigrationActionExprArg_Pattern, RegexOptions.Compiled);
		private const string ConformingMigrationActionExprArg_Pattern = $@"^_\s*=>\s*_\s*.\s*({(DEBUG?"":"?:")}Up|Down)grade_v(\d+)w(\d+)_To_v(\d+)w(\d+)";
		private const int ConformingMigrationActionExprArg_VersionGroupOffset = DEBUG ? 1 : 0;
	}
}
