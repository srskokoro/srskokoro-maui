namespace Kokoro.Internal.IO.Extensions;
using Kokoro.Common.Util;
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.InteropServices;

internal static partial class StreamExtensions {

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
