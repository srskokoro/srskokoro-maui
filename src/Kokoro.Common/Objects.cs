namespace Kokoro.Common;
using System;

internal static class Objects {

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static object OrDBNull(this object? o) {
		if (o != null) return o;
		return DBNull.Value;
	}
}
