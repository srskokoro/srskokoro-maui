namespace Kokoro.Common.IO.Extensions;
using Kokoro.Common.Util;

internal static partial class StreamExtensions {

	public static byte[] ReadFully(this Stream stream) {
		MemoryStream mstream = new();
		stream.CopyTo(mstream);
		return mstream.ToArray();
	}

	// --

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[SkipLocalsInit]
	public static ulong ReadVarInt(this Stream stream) {
		Span<byte> buffer = stackalloc byte[VarInts.MaxLength64];
		int sread = stream.Read(buffer);

		int vread = VarInts.Read(buffer[..sread], out ulong result);
		stream.Position += vread - sread;

		if (vread != 0) {
			return result;
		} else {
			return StreamUtils.E_EndOfStreamRead_InvOp<ulong>();
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[SkipLocalsInit]
	public static ulong ReadVarIntOrZero(this Stream stream) {
		Span<byte> buffer = stackalloc byte[VarInts.MaxLength64];
		int sread = stream.Read(buffer);

		int vread = VarInts.Read(buffer[..sread], out ulong result);
		stream.Position += vread - sread;

		return result;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[SkipLocalsInit]
	public static int TryReadVarInt(this Stream stream, out ulong result) {
		Span<byte> buffer = stackalloc byte[VarInts.MaxLength64];
		int sread = stream.Read(buffer);

		int vread = VarInts.Read(buffer[..sread], out result);
		stream.Position += vread - sread;

		return vread;
	}
}
