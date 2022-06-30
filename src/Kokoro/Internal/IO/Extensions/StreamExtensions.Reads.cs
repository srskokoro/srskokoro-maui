namespace Kokoro.Internal.IO.Extensions;
using Kokoro.Common.IO;
using System.Buffers.Binary;
using System.Runtime.InteropServices;

internal static partial class StreamExtensions {

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[SkipLocalsInit]
	public static ulong ReadUIntX(this Stream stream, int sizeOfUIntX) {
		const int MaxSize = sizeof(ulong);
		if ((uint)sizeOfUIntX > (uint)MaxSize) {
			ThrowHelper.ThrowArgumentOutOfRangeException();
		}

		ulong result = 0;
		var buffer = MemoryMarshal.CreateSpan(ref U.Add(
			ref U.As<ulong, byte>(ref result), MaxSize - sizeOfUIntX), sizeOfUIntX);

		int sread = stream.Read(buffer);
		if (sread != sizeOfUIntX)
			StreamUtils.E_EndOfStreamRead_InvOp();

		// Needed since the `UIntX` in the stream is assumed big-endian
		if (BitConverter.IsLittleEndian) {
			return BinaryPrimitives.ReverseEndianness(result);
		}
		return result;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[SkipLocalsInit]
	public static uint ReadUIntXAsUInt32(this Stream stream, int sizeOfUIntX) {
		const int MaxSize = sizeof(uint);
		if ((uint)sizeOfUIntX > (uint)MaxSize) {
			ThrowHelper.ThrowArgumentOutOfRangeException();
		}

		uint result = 0;
		var buffer = MemoryMarshal.CreateSpan(ref U.Add(
			ref U.As<uint, byte>(ref result), MaxSize - sizeOfUIntX), sizeOfUIntX);

		int sread = stream.Read(buffer);
		if (sread != sizeOfUIntX)
			StreamUtils.E_EndOfStreamRead_InvOp();

		// Needed since the `UIntX` in the stream is assumed big-endian
		if (BitConverter.IsLittleEndian) {
			return BinaryPrimitives.ReverseEndianness(result);
		}
		return result;
	}
}
