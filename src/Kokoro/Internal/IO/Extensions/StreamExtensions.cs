namespace Kokoro.Internal.IO.Extensions;
using Kokoro.Common.Util;
using System.Buffers.Binary;
using System.Runtime.InteropServices;

internal static class StreamExtensions {

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[SkipLocalsInit]
	public static ulong ReadVarInt(this Stream stream) {
		Span<byte> buffer = stackalloc byte[9];
		int sread = stream.Read(buffer);

		int vread = VarInts.Read(buffer, out ulong result);
		stream.Position += vread - sread;

		if (vread != 0) {
			return result;
		} else {
			E_EndOfStream_InvOp();
			return 0;
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[SkipLocalsInit]
	public static ulong ReadVarIntOrZero(this Stream stream) {
		Span<byte> buffer = stackalloc byte[9];
		int sread = stream.Read(buffer);

		int vread = VarInts.Read(buffer, out ulong result);
		stream.Position += vread - sread;

		return result;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[SkipLocalsInit]
	public static int TryReadVarInt(this Stream stream, out ulong result) {
		Span<byte> buffer = stackalloc byte[9];
		int sread = stream.Read(buffer);

		int vread = VarInts.Read(buffer, out result);
		stream.Position += vread - sread;

		return vread;
	}

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
			E_EndOfStream_InvOp();

		// Needed since the `UIntX` in the stream is assumed big-endian
		if (BitConverter.IsLittleEndian) {
			return BinaryPrimitives.ReverseEndianness(result);
		}
		return result;
	}

	[DoesNotReturn]
	private static void E_EndOfStream_InvOp()
		=> throw new InvalidOperationException("The stream didn't contain enough data to read the requested item.");

	// --

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static FieldVal ReadFieldVal(this Stream stream)
		=> stream.ReadFieldVal(stream.Length);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[SkipLocalsInit]
	public static FieldVal ReadFieldVal(this Stream stream, long length) {
		if (length > 0) {
			int vread = stream.TryReadVarInt(out ulong valSpec);
			if (vread != 0) {
				var data = new byte[length - vread];
				int typeHint = (int)valSpec - 1;
				return new FieldVal(typeHint, data);
			}
		}
		return FieldVal.Null;
	}
}
