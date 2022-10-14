namespace Kokoro.Internal.IO.Extensions;
using System.Runtime.InteropServices;

internal static partial class StreamExtensions {

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[SkipLocalsInit]
	public static void WriteUInt64AsUIntXLE(this Stream stream, ulong value, int sizeOfUIntX) {
		const int MaxSize = sizeof(ulong);
		if ((uint)sizeOfUIntX > (uint)MaxSize) {
			ThrowHelper.ThrowArgumentOutOfRangeException();
		}

		// Needed since the `UIntX` in the stream is little-endian.
		// Toggle into little-endian (NOP if already little-endian).
		var tmp = value.LittleEndian();

		var buffer = MemoryMarshal.CreateReadOnlySpan(
			ref U.As<ulong, byte>(ref tmp), sizeOfUIntX);

		stream.Write(buffer);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[SkipLocalsInit]
	public static void WriteUInt32AsUIntXLE(this Stream stream, uint value, int sizeOfUIntX) {
		const int MaxSize = sizeof(uint);
		if ((uint)sizeOfUIntX > (uint)MaxSize) {
			ThrowHelper.ThrowArgumentOutOfRangeException();
		}

		// Needed since the `UIntX` in the stream is little-endian.
		// Toggle into little-endian (NOP if already little-endian).
		var tmp = value.LittleEndian();

		var buffer = MemoryMarshal.CreateReadOnlySpan(
			ref U.As<uint, byte>(ref tmp), sizeOfUIntX);

		stream.Write(buffer);
	}
}
