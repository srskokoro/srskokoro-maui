namespace Kokoro.Common.Sqlite;

internal static class SqliteUtils {

	/// <summary>
	/// Deletes the sqlite database along with its journal/WAL files. This will
	/// delete such files, even if the database file itself no longer exists.
	/// </summary>
	/// <remarks>
	/// Also useful when avoiding a dangerous scenario where the journal/WAL
	/// file could end up being mispaired against a different/new database.
	/// <para>
	/// See, “<see href="https://www.sqlite.org/howtocorrupt.html#_unlinking_or_renaming_a_database_file_while_in_use">2.4.
	/// Unlinking or renaming a database file while in use | How To Corrupt An
	/// SQLite Database File | SQLite</see>”
	/// </para>
	/// </remarks>
	public static void DeleteSqliteDb(string path) {
		File.Delete(path);
		File.Delete($"{path}-wal");
		File.Delete($"{path}-shm");
		File.Delete($"{path}-journal");

		// Also delete files for when SQLite is using 8.3 filenames
		// - See, https://www.sqlite.org/shortnames.html
		{
			var pathSpan = path.AsSpan();
			var extLen = Path.GetExtension(pathSpan).Length; // NOTE: Includes '.'
			if (extLen is > 1 and <= 4) {
				if (Path.GetFileNameWithoutExtension(pathSpan).Length <= 8) {
					var pathNoExt = pathSpan[..^extLen];
					File.Delete($"{pathNoExt}.wal");
					File.Delete($"{pathNoExt}.shm");
					File.Delete($"{pathNoExt}.nal");
				}
			}
		}

		// Don't know how to delete super-journal files, so just end here.
		//
		// Still, it should be safe to not delete super-journal files. Quote
		// from, “5.0 Writing to a database file | File Locking And Concurrency
		// In SQLite Version 3”:
		//
		// > 5. … The name of the super-journal is arbitrary. (The current
		// > implementation appends random suffixes to the name of the main
		// > database file until it finds a name that does not previously
		// exist.) …
		//
		// See, https://web.archive.org/web/20220407150600/https://sqlite.org/lockingv3.html#writing:~:text=The%20current%20implementation%20appends,exist%2E
	}
}
