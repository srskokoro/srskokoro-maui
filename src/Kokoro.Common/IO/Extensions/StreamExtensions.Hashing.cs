namespace Kokoro.Common.IO.Extensions;
using global::Blake2Fast.Implementation;
using System.Buffers;

internal static partial class StreamExtensions {

	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	[SkipLocalsInit]
	public static void FeedFullyTo(this Stream source, ref Blake2bHashState hasher, int bufferSize = StreamUtils.DefaultCopyBufferSize) {
		byte[] buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
		try {
			int bytesRead;
			while ((bytesRead = source.Read(buffer, 0, buffer.Length)) != 0) {
				hasher.Update(buffer.AsSpan(0, bytesRead));
			}
		} finally {
			ArrayPool<byte>.Shared.Return(buffer);
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	[SkipLocalsInit]
	public static void FeedPartlyTo(this Stream source, ref Blake2bHashState hasher, int count, int bufferSize = StreamUtils.DefaultCopyBufferSize) {
		byte[] buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
		try {
			int remaining = count;
			while (remaining > buffer.Length) {
				int bytesRead = source.Read(buffer, 0, buffer.Length);
				if (bytesRead != 0) {
					hasher.Update(buffer.AsSpan(0, bytesRead));
					remaining -= bytesRead;
				} else {
					goto Done;
				}
			}
			// --
			{
				int bytesRead;
				while ((bytesRead = source.Read(buffer, 0, remaining)) != 0) {
					hasher.Update(buffer.AsSpan(0, bytesRead));
				}
			}
		Done:
			// ^ Label must still be within the `try…finally` block, so that the
			// `goto` statement can simply become a conditional jump forward.
			;
		} finally {
			ArrayPool<byte>.Shared.Return(buffer);
		}
	}
}
