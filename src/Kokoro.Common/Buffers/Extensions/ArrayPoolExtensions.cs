namespace Kokoro.Common.Buffers.Extensions;
using System.Buffers;

internal static class ArrayPoolExtensions {

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void ReturnClearingReferences<T>(this ArrayPool<T> pool, T[] array) {
		pool.Return(array, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<T>());
	}
}
