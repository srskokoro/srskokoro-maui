namespace Kokoro.Common.IO.Extensions;
using Kokoro.Common.Util;
using System.Buffers;

internal static partial class StreamExtensions {

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static object OrNullStream(this Stream? stream) {
		// Favors the non-null case. The null case becomes the conditional jump forward.
		if (stream != null) return stream;
		return Stream.Null;
	}

	// --

	private const int DefaultCopyBufferSize = 8192;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void CopyPartlyTo(this Stream source, Stream destination, int count)
		=> source.CopyPartlyTo(destination, count, DefaultCopyBufferSize);

	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	[SkipLocalsInit]
	public static void CopyPartlyTo(this Stream source, Stream destination, int count, int bufferSize) {
		byte[] buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
		try {
			int remaining = count;
			while (remaining > buffer.Length) {
				int bytesRead = source.Read(buffer, 0, buffer.Length);
				if (bytesRead != 0) {
					destination.Write(buffer, 0, bytesRead);
					remaining -= bytesRead;
				} else {
					goto Done;
				}
			}
			// --
			{
				int bytesRead;
				while ((bytesRead = source.Read(buffer, 0, remaining)) != 0) {
					destination.Write(buffer, 0, bytesRead);
				}
			}
		Done:
			;
		} finally {
			ArrayPool<byte>.Shared.Return(buffer);
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void CopyPartlyTo(this Stream source, Stream destination, long count)
		=> source.CopyPartlyTo(destination, count, DefaultCopyBufferSize);

	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	[SkipLocalsInit]
	public static void CopyPartlyTo(this Stream source, Stream destination, long count, int bufferSize) {
		byte[] buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
		try {
			long remaining = count;
			while (remaining > buffer.Length) {
				int bytesRead = source.Read(buffer, 0, buffer.Length);
				if (bytesRead != 0) {
					destination.Write(buffer, 0, bytesRead);
					remaining -= bytesRead;
				} else {
					goto Done;
				}
			}
			// --
			{
				int remainingAsInt = (int)remaining;
				int bytesRead;
				while ((bytesRead = source.Read(buffer, 0, remainingAsInt)) != 0) {
					destination.Write(buffer, 0, bytesRead);
				}
			}
		Done:
			;
		} finally {
			ArrayPool<byte>.Shared.Return(buffer);
		}
	}

	// --

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[SkipLocalsInit]
	public static void WriteVarInt(this Stream stream, ulong value) {
		Span<byte> buffer = stackalloc byte[VarInts.MaxLength64];
		int vlen = VarInts.Write(buffer, value);
		stream.Write(buffer[..vlen]);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[SkipLocalsInit]
	public static void WriteVarInt(this Stream stream, uint value) {
		Span<byte> buffer = stackalloc byte[VarInts.MaxLength32];
		int vlen = VarInts.Write(buffer, value);
		stream.Write(buffer[..vlen]);
	}
}
