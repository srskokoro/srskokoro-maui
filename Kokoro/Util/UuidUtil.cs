using System.Runtime.CompilerServices;

namespace Kokoro.Util;

public static class UuidUtil {

	/// <summary>
	/// Toggles between GUID and UUID layout.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static byte[] GuidUuidSwap(byte[] bytes) {
		GuidUuidSwap(bytes.AsSpan());
		return bytes;
	}

	/// <summary>
	/// Toggles between GUID and UUID layout.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	public static void GuidUuidSwap(Span<byte> bytes) {
		byte x;

		x = bytes[0]; bytes[0] = bytes[3]; bytes[3] = x;
		x = bytes[1]; bytes[1] = bytes[2]; bytes[2] = x;

		x = bytes[4]; bytes[4] = bytes[5]; bytes[5] = x;
		x = bytes[6]; bytes[6] = bytes[7]; bytes[7] = x;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Guid GuidFromUuid(byte[] uuidBytes)
		=> GuidFromUuid(uuidBytes.AsSpan());

	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	public static Guid GuidFromUuid(ReadOnlySpan<byte> uuidBytes) {
		#pragma warning disable format
		return new(stackalloc byte[16] {
			// Swap bytes
			uuidBytes[3], uuidBytes[2], uuidBytes[1], uuidBytes[0],
			uuidBytes[5], uuidBytes[4],
			uuidBytes[7], uuidBytes[6],
			// Not swapped
			uuidBytes[8], uuidBytes[9],
			uuidBytes[10], uuidBytes[11], uuidBytes[12], uuidBytes[13], uuidBytes[14], uuidBytes[15]
		});
		#pragma warning restore format
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static byte[] ToUuidByteArray(in this Guid guid) {
		return GuidUuidSwap(guid.ToByteArray());
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool TryWriteUuidBytes(in this Guid guid, Span<byte> destination) {
		bool r = guid.TryWriteBytes(destination);
		if (r) GuidUuidSwap(destination);
		return r;
	}
}
