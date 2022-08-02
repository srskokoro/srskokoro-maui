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

	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	[SkipLocalsInit]
	public static int CopyPartlyTo(this Stream source, Stream destination, int count, int bufferSize = StreamUtils.DefaultCopyBufferSize) {
		int remaining = count;
		Debug.Assert(bufferSize > 0);
		byte[] buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
		try {
			while (remaining > buffer.Length) {
				int bytesRead = source.Read(buffer, 0, buffer.Length);
				if (bytesRead != 0) {
					destination.Write(buffer, 0, bytesRead);
					remaining -= bytesRead;
				} else {
					goto Done;
				}
			}
			do {
				int bytesRead = source.Read(buffer, 0, remaining);
				if (bytesRead != 0) {
					destination.Write(buffer, 0, bytesRead);
					remaining -= bytesRead;
				} else {
					goto Done;
				}
			} while (remaining != 0);
		Done:
			// ^ Label must still be within the `try…finally` block, so that the
			// `goto` statement can simply become a conditional jump forward.
			;
		} finally {
			ArrayPool<byte>.Shared.Return(buffer);
		}
		return remaining;
	}

	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	[SkipLocalsInit]
	public static long CopyPartlyTo(this Stream source, Stream destination, long count, int bufferSize = StreamUtils.DefaultCopyBufferSize) {
		long remaining = count;
		Debug.Assert(bufferSize > 0);
		byte[] buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
		try {
			while (remaining > buffer.Length) {
				int bytesRead = source.Read(buffer, 0, buffer.Length);
				if (bytesRead != 0) {
					destination.Write(buffer, 0, bytesRead);
					remaining -= bytesRead;
				} else {
					goto Done;
				}
			}
			do {
				int bytesRead = source.Read(buffer, 0, (int)remaining);
				if (bytesRead != 0) {
					destination.Write(buffer, 0, bytesRead);
					remaining -= bytesRead;
				} else {
					goto Done;
				}
			} while (remaining != 0);
		Done:
			// ^ Label must still be within the `try…finally` block, so that the
			// `goto` statement can simply become a conditional jump forward.
			;
		} finally {
			ArrayPool<byte>.Shared.Return(buffer);
		}
		return remaining;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void ClearFully(this Stream stream, int bufferSize = StreamUtils.DefaultCopyBufferSize)
		=> stream.ClearPartly(stream.Length, bufferSize);

	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	[SkipLocalsInit]
	public static void ClearPartly(this Stream stream, int count, int bufferSize = StreamUtils.DefaultCopyBufferSize) {
		Debug.Assert(bufferSize > 0);
		byte[] buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
		try {
			buffer.AsDangerousSpan().Clear(); // Zero-fill
			int remaining = count;
			while (remaining > buffer.Length) {
				stream.Write(buffer, 0, buffer.Length);
				remaining -= buffer.Length;
			}
			stream.Write(buffer, 0, remaining);
		} finally {
			ArrayPool<byte>.Shared.Return(buffer);
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	[SkipLocalsInit]
	public static void ClearPartly(this Stream stream, long count, int bufferSize = StreamUtils.DefaultCopyBufferSize) {
		Debug.Assert(bufferSize > 0);
		byte[] buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
		try {
			buffer.AsDangerousSpan().Clear(); // Zero-fill
			long remaining = count;
			while (remaining > buffer.Length) {
				stream.Write(buffer, 0, buffer.Length);
				remaining -= buffer.Length;
			}
			stream.Write(buffer, 0, (int)remaining);
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
