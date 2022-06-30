namespace Kokoro.Common.Buffers.Extensions;
using System.Runtime.InteropServices;

internal static class ArrayExtensions {

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Span<T> AsDangerousSpan<T>(this T[] array)
		=> array.AsDangerousSpanShortened(array.Length);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Span<T> AsDangerousSpan<T>(this T[] array, int start)
		=> array.AsDangerousSpan(start, array.Length);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Span<T> AsDangerousSpan<T>(this T[] array, int start, int length) {
		/// See note in <see cref="AsDangerousSpanShortened{T}(T[], int)"/>

		ref T r0 = ref array.DangerousGetReferenceAt(start);
		return MemoryMarshal.CreateSpan(ref r0, length);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Span<T> AsDangerousSpanShortened<T>(this T[] array, int length) {
		// NOTE: As of writing this, the following produces more efficient asm
		// than `new Span<T>(array, 0, array.Length)` -- with the latter being
		// more efficient than `new Span<T>(array)` or casting to `Span<T>`.
		//
		// The approach was borrowed from, https://github.com/CommunityToolkit/dotnet/blob/v8.0.0-preview3/CommunityToolkit.HighPerformance/Buffers/SpanOwner%7BT%7D.cs#L139

		ref T r0 = ref array.DangerousGetReference();
		return MemoryMarshal.CreateSpan(ref r0, length);
	}
}
