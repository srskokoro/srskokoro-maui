namespace Kokoro.Internal;

internal static class RowIds {

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static object? Box(long rowid) {
		if (rowid != 0)
			return rowid;
		return null;
	}
}
