﻿namespace Kokoro.Internal.IO.Extensions;
using Kokoro.Common.IO;
using System.Runtime.InteropServices;

internal static partial class StreamExtensions {

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[SkipLocalsInit]
	public static ulong ReadUIntXLE(this Stream stream, int sizeOfUIntX) {
		const int MaxSize = sizeof(ulong);
		if ((uint)sizeOfUIntX > (uint)MaxSize) {
			goto CapSizeToMax;
		}

	DoRead:
		ulong result = 0;
		var buffer = MemoryMarshal.CreateSpan(
			ref U.As<ulong, byte>(ref result), sizeOfUIntX);

		int sread = stream.Read(buffer);
		if (sread != sizeOfUIntX)
			StreamUtils.E_EndOfStreamRead_InvOp();

		// Needed since the `UIntX` in the stream is little-endian.
		// Toggle from little-endian (NOP if already little-endian).
		return result.LittleEndian();

	CapSizeToMax:
		sizeOfUIntX = MaxSize;
		goto DoRead;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[SkipLocalsInit]
	public static uint ReadUIntXLEAsUInt32(this Stream stream, int sizeOfUIntX) {
		const int MaxSize = sizeof(uint);
		if ((uint)sizeOfUIntX > (uint)MaxSize) {
			goto CapSizeToMax;
		}

	DoRead:
		uint result = 0;
		var buffer = MemoryMarshal.CreateSpan(
			ref U.As<uint, byte>(ref result), sizeOfUIntX);

		int sread = stream.Read(buffer);
		if (sread != sizeOfUIntX)
			StreamUtils.E_EndOfStreamRead_InvOp();

		// Needed since the `UIntX` in the stream is little-endian.
		// Toggle from little-endian (NOP if already little-endian).
		return result.LittleEndian();

	CapSizeToMax:
		sizeOfUIntX = MaxSize;
		goto DoRead;
	}
}
