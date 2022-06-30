namespace Kokoro.Common.Buffers;
using System.Buffers;

/// <summary>
/// A highly stripped-down version of <see cref="CommunityToolkit.HighPerformance.Buffers.SpanOwner{T}"/>
/// that always rents from <see cref="ArrayPool{T}.Shared"/> and ensures that
/// rented arrays containing reference types are always cleared when returning
/// them to the pool.
/// </summary>
/// <typeparam name="T">The type of items to store in the current instance.</typeparam>
internal readonly struct BufferRenter<T> : IDisposable {

	public readonly T[] Array;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public BufferRenter(int length) {
		Array = ArrayPool<T>.Shared.Rent(length);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public BufferRenter(int length, out Span<T> span) {
		span = (Array = ArrayPool<T>.Shared.Rent(length)).AsDangerousSpanShortened(length);
	}

	/// <inheritdoc/>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void Dispose() {
		ArrayPool<T>.Shared.Return(
			Array,
			clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<T>()
		);
	}
}
