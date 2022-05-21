namespace Kokoro.Common.Sqlite.Extensions;
using Microsoft.Data.Sqlite;

internal static class SqliteParameterCollectionExtensions {

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static SqliteParameterCollection Cleared(this SqliteParameterCollection col) {
		col.Clear();
		return col;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static SqliteParameterCollection AddChain(this SqliteParameterCollection col, SqliteParameter param1) {
		col.Add(param1);
		return col;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static SqliteParameterCollection AddChain(this SqliteParameterCollection col, SqliteParameter param1, SqliteParameter param2) {
		col.Add(param1);
		col.Add(param2);
		return col;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static SqliteParameterCollection AddChain(this SqliteParameterCollection col, SqliteParameter param1, SqliteParameter param2, SqliteParameter param3) {
		col.Add(param1);
		col.Add(param2);
		col.Add(param3);
		return col;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static SqliteParameterCollection AddChain(this SqliteParameterCollection col, SqliteParameter param1, SqliteParameter param2, SqliteParameter param3, SqliteParameter param4) {
		col.Add(param1);
		col.Add(param2);
		col.Add(param3);
		col.Add(param4);
		return col;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static SqliteParameterCollection AddChain(this SqliteParameterCollection col, SqliteParameter param1, SqliteParameter param2, SqliteParameter param3, SqliteParameter param4, SqliteParameter param5) {
		col.Add(param1);
		col.Add(param2);
		col.Add(param3);
		col.Add(param4);
		col.Add(param5);
		return col;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static SqliteParameterCollection AddChain(this SqliteParameterCollection col, SqliteParameter param1, SqliteParameter param2, SqliteParameter param3, SqliteParameter param4, SqliteParameter param5, SqliteParameter param6) {
		col.Add(param1);
		col.Add(param2);
		col.Add(param3);
		col.Add(param4);
		col.Add(param5);
		col.Add(param6);
		return col;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static SqliteParameterCollection AddChain(this SqliteParameterCollection col, SqliteParameter param1, SqliteParameter param2, SqliteParameter param3, SqliteParameter param4, SqliteParameter param5, SqliteParameter param6, SqliteParameter param7) {
		col.Add(param1);
		col.Add(param2);
		col.Add(param3);
		col.Add(param4);
		col.Add(param5);
		col.Add(param6);
		col.Add(param7);
		return col;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static SqliteParameterCollection AddChain(this SqliteParameterCollection col, SqliteParameter param1, SqliteParameter param2, SqliteParameter param3, SqliteParameter param4, SqliteParameter param5, SqliteParameter param6, SqliteParameter param7, SqliteParameter param8) {
		col.Add(param1);
		col.Add(param2);
		col.Add(param3);
		col.Add(param4);
		col.Add(param5);
		col.Add(param6);
		col.Add(param7);
		col.Add(param8);
		return col;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static SqliteParameterCollection AddChain(this SqliteParameterCollection col, SqliteParameter param1, SqliteParameter param2, SqliteParameter param3, SqliteParameter param4, SqliteParameter param5, SqliteParameter param6, SqliteParameter param7, SqliteParameter param8, SqliteParameter param9) {
		col.Add(param1);
		col.Add(param2);
		col.Add(param3);
		col.Add(param4);
		col.Add(param5);
		col.Add(param6);
		col.Add(param7);
		col.Add(param8);
		col.Add(param9);
		return col;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static SqliteParameterCollection AddChain(this SqliteParameterCollection col, SqliteParameter param1, SqliteParameter param2, SqliteParameter param3, SqliteParameter param4, SqliteParameter param5, SqliteParameter param6, SqliteParameter param7, SqliteParameter param8, SqliteParameter param9, SqliteParameter param10) {
		col.Add(param1);
		col.Add(param2);
		col.Add(param3);
		col.Add(param4);
		col.Add(param5);
		col.Add(param6);
		col.Add(param7);
		col.Add(param8);
		col.Add(param9);
		col.Add(param10);
		return col;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static SqliteParameterCollection AddChain(this SqliteParameterCollection col, SqliteParameter param1, SqliteParameter param2, SqliteParameter param3, SqliteParameter param4, SqliteParameter param5, SqliteParameter param6, SqliteParameter param7, SqliteParameter param8, SqliteParameter param9, SqliteParameter param10, SqliteParameter param11) {
		col.Add(param1);
		col.Add(param2);
		col.Add(param3);
		col.Add(param4);
		col.Add(param5);
		col.Add(param6);
		col.Add(param7);
		col.Add(param8);
		col.Add(param9);
		col.Add(param10);
		col.Add(param11);
		return col;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static SqliteParameterCollection AddChain(this SqliteParameterCollection col, SqliteParameter param1, SqliteParameter param2, SqliteParameter param3, SqliteParameter param4, SqliteParameter param5, SqliteParameter param6, SqliteParameter param7, SqliteParameter param8, SqliteParameter param9, SqliteParameter param10, SqliteParameter param11, SqliteParameter param12) {
		col.Add(param1);
		col.Add(param2);
		col.Add(param3);
		col.Add(param4);
		col.Add(param5);
		col.Add(param6);
		col.Add(param7);
		col.Add(param8);
		col.Add(param9);
		col.Add(param10);
		col.Add(param11);
		col.Add(param12);
		return col;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static SqliteParameterCollection AddChain(this SqliteParameterCollection col, SqliteParameter param1, SqliteParameter param2, SqliteParameter param3, SqliteParameter param4, SqliteParameter param5, SqliteParameter param6, SqliteParameter param7, SqliteParameter param8, SqliteParameter param9, SqliteParameter param10, SqliteParameter param11, SqliteParameter param12, SqliteParameter param13) {
		col.Add(param1);
		col.Add(param2);
		col.Add(param3);
		col.Add(param4);
		col.Add(param5);
		col.Add(param6);
		col.Add(param7);
		col.Add(param8);
		col.Add(param9);
		col.Add(param10);
		col.Add(param11);
		col.Add(param12);
		col.Add(param13);
		return col;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static SqliteParameterCollection AddChain(this SqliteParameterCollection col, SqliteParameter param1, SqliteParameter param2, SqliteParameter param3, SqliteParameter param4, SqliteParameter param5, SqliteParameter param6, SqliteParameter param7, SqliteParameter param8, SqliteParameter param9, SqliteParameter param10, SqliteParameter param11, SqliteParameter param12, SqliteParameter param13, SqliteParameter param14) {
		col.Add(param1);
		col.Add(param2);
		col.Add(param3);
		col.Add(param4);
		col.Add(param5);
		col.Add(param6);
		col.Add(param7);
		col.Add(param8);
		col.Add(param9);
		col.Add(param10);
		col.Add(param11);
		col.Add(param12);
		col.Add(param13);
		col.Add(param14);
		return col;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static SqliteParameterCollection AddChain(this SqliteParameterCollection col, SqliteParameter param1, SqliteParameter param2, SqliteParameter param3, SqliteParameter param4, SqliteParameter param5, SqliteParameter param6, SqliteParameter param7, SqliteParameter param8, SqliteParameter param9, SqliteParameter param10, SqliteParameter param11, SqliteParameter param12, SqliteParameter param13, SqliteParameter param14, SqliteParameter param15) {
		col.Add(param1);
		col.Add(param2);
		col.Add(param3);
		col.Add(param4);
		col.Add(param5);
		col.Add(param6);
		col.Add(param7);
		col.Add(param8);
		col.Add(param9);
		col.Add(param10);
		col.Add(param11);
		col.Add(param12);
		col.Add(param13);
		col.Add(param14);
		col.Add(param15);
		return col;
	}
}
