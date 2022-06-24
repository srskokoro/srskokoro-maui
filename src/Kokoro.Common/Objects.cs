namespace Kokoro.Common;
using System;

internal static class Objects {

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static object OrDBNull(this object? o) {
		// Favors the non-null case. The null case becomes the conditional jump forward.
		if (o != null) return o;
		return DBNull.Value;
	}
}
