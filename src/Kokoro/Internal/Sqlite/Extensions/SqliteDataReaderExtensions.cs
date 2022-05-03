namespace Kokoro.Internal.Sqlite.Extensions;
using Microsoft.Data.Sqlite;

internal static class SqliteDataReaderExtensions {

	public static UniqueId GetUniqueId(this SqliteDataReader reader, int ordinal) {
		using var s = reader.GetStream(ordinal);
		return s.Read<UniqueId>();
	}
}
