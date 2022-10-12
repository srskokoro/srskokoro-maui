namespace Kokoro.DataProtocols;

internal static class Prot_v0w1 {

	public const string NameId = nameof(NameId);

	public const string Item = nameof(Item);
	public const string ItemToColdStore = nameof(ItemToColdStore);
	public const string ItemToFloatingField = nameof(ItemToFloatingField);

	public const string Schema = nameof(Schema);
	public const string SchemaToField = nameof(SchemaToField);
	public const string SchemaToClass = nameof(SchemaToClass);

	public const string Class = nameof(Class);
	public const string ClassToField = nameof(ClassToField);
	public const string ClassToInclude = nameof(ClassToInclude);
	public const string ClassToEnumElem = nameof(ClassToEnumElem);

	// --

	internal static class Fs {

		private static readonly char _ = Path.DirectorySeparatorChar;

		// NOTE: Aside from ensuring that the temporary files used by SQLite are
		// kept together in one place, keeping a database file in its own
		// private subdirectory can also help avoid corruption in rare cases on
		// some filesystems.
		//
		// See,
		// - https://web.archive.org/web/20220317081844/https://www.sqlite.org/atomiccommit.html#_deleting_or_renaming_a_hot_journal:~:text=consider%20putting,subdirectory
		// - https://web.archive.org/web/20220407150600/https://www.sqlite.org/lockingv3.html#how_to_corrupt:~:text=could%20happen%2E-,The%20best%20defenses,themselves
		//
		public static string CollectionDbDir => $"col.db";
		public static string CollectionDb => $"{CollectionDbDir}{_}main";

		public static string Extensions => $"ext";
		public static string Media => $"media";

		public static string ConfigDbDir => $"conf.db";
		public static string ConfigDb => $"{ConfigDbDir}{_}main";
	}
}
