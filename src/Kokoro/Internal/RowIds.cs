namespace Kokoro.Internal;

internal static class RowIds {

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Box<long>? Box(long rowid) {
		if (rowid != 0)
			return rowid;
		return null;
	}
}
