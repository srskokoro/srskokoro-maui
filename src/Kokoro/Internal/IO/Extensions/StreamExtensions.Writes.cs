namespace Kokoro.Internal.IO.Extensions;
using Kokoro.Common.Util;
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.InteropServices;

internal static partial class StreamExtensions {

	private const int DefaultCopyBufferSize = 8192;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void CopyPartlyTo(this Stream source, Stream destination, int count)
		=> source.CopyPartlyTo(destination, count, DefaultCopyBufferSize);

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

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[SkipLocalsInit]
	public static void WriteUIntX(this Stream stream, ulong value, int sizeOfUIntX) {
		const int MaxSize = sizeof(ulong);
		if ((uint)sizeOfUIntX > (uint)MaxSize) {
			ThrowHelper.ThrowArgumentOutOfRangeException();
		}

		// Needed since the `UIntX` in the stream is assumed big-endian
		var tmp = BitConverter.IsLittleEndian
			? BinaryPrimitives.ReverseEndianness(value) : value;

		var buffer = MemoryMarshal.CreateReadOnlySpan(ref U.Add(
			ref U.As<ulong, byte>(ref tmp), MaxSize - sizeOfUIntX), sizeOfUIntX);

		stream.Write(buffer);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[SkipLocalsInit]
	public static void WriteUIntXUpTo4Bytes(this Stream stream, uint value, int sizeOfUIntX) {
		const int MaxSize = sizeof(uint);
		if ((uint)sizeOfUIntX > (uint)MaxSize) {
			ThrowHelper.ThrowArgumentOutOfRangeException();
		}

		// Needed since the `UIntX` in the stream is assumed big-endian
		var tmp = BitConverter.IsLittleEndian
			? BinaryPrimitives.ReverseEndianness(value) : value;

		var buffer = MemoryMarshal.CreateReadOnlySpan(ref U.Add(
			ref U.As<uint, byte>(ref tmp), MaxSize - sizeOfUIntX), sizeOfUIntX);

		stream.Write(buffer);
	}
}
