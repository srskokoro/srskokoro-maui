namespace Kokoro.Internal;

internal static class RowIds {

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static object? DBBox(long rowid) {
		if (rowid != 0) return rowid;
		return DBNull.Value;
	}
}
