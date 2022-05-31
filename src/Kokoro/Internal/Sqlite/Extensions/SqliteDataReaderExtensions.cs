﻿namespace Kokoro.Internal.Sqlite.Extensions;
using Microsoft.Data.Sqlite;

internal static class SqliteDataReaderExtensions {

	public static UniqueId GetUniqueId(this SqliteDataReader reader, int ordinal) {
		using var s = reader.GetStream(ordinal);
		return s.Read<UniqueId>();
	}

	public static byte[] GetBytes(this SqliteDataReader reader, int ordinal) {
		if (reader.GetValue(ordinal) is byte[] bytes) {
			return bytes;
		}
		return Array.Empty<byte>();
	}
}
