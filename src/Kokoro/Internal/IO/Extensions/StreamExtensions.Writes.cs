﻿namespace Kokoro.Internal.IO.Extensions;
using System;
using System.Buffers.Binary;
using System.Runtime.InteropServices;

internal static partial class StreamExtensions {

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[SkipLocalsInit]
	public static void WriteUInt64AsUIntX(this Stream stream, ulong value, int sizeOfUIntX) {
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
	public static void WriteUInt32AsUIntX(this Stream stream, uint value, int sizeOfUIntX) {
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
