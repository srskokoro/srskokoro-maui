﻿namespace Kokoro.Common.Sqlite.Extensions;
using Microsoft.Data.Sqlite;

internal static class SqliteDataReaderExtensions {

	[Conditional("DEBUG")]
	public static void DebugAssert_Name(this SqliteDataReader reader, int ordinal, string name) {
		string actualName = reader.GetName(ordinal);
		Debug.Assert(actualName == name, $"Column {ordinal}; Expected name: {name}; Actual name: {actualName}");
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[Conditional("DEBUG")]
	public static void DAssert_Name(this SqliteDataReader reader, int ordinal, string name)
		=> reader.DebugAssert_Name(ordinal, name);
}
