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


	[Obsolete("Shouldn't use.", error: true)]
	public BufferRenter() => throw new NotSupportedException("Shouldn't use.");

	#region Private constructors

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private BufferRenter(int length)
		=> Array = ArrayPool<T>.Shared.Rent(length);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private BufferRenter(int length, out T[] array)
		=> array = Array = ArrayPool<T>.Shared.Rent(length);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private BufferRenter(int length, out Span<T> span)
		=> span = (Array = ArrayPool<T>.Shared.Rent(length)).AsDangerousSpanShortened(length);

	#endregion


	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static BufferRenter<T> Create(int length) => new(length);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static BufferRenter<T> Create(int length, out T[] array) => new(length, out array);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static BufferRenter<T> CreateSpan(int length, out Span<T> span) => new(length, out span);


	/// <inheritdoc/>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void Dispose() => ArrayPool<T>.Shared.ReturnClearingReferences(Array);
}
