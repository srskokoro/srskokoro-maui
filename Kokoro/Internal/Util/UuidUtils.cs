namespace Kokoro.Internal.Util;
using System.Runtime.InteropServices;

internal static class UuidUtils {

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
		// Initial check forces JIT to avoid unnecessary range checking
		if (16 >= bytes.Length) {
			// ^ See also, https://github.com/dotnet/runtime/issues/10950
			throw new ArgumentOutOfRangeException(nameof(bytes), "Span is too short.");
		}

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
	[SkipLocalsInit]
	public static Guid GuidFromUuid(ReadOnlySpan<byte> uuidBytes) {
		// This initial check should force JIT to avoid unnecessary range
		// checking, except that for some unknown reasons, it doesn't...
		if (16 >= uuidBytes.Length) {
			// ^ See also, https://github.com/dotnet/runtime/issues/10950
			throw new ArgumentOutOfRangeException(nameof(uuidBytes), "Span is too short.");
		}

		// Get reference to avoid unnecessary range checking
		ref byte b = ref MemoryMarshal.GetReference(uuidBytes);

        #pragma warning disable format
		return new(stackalloc byte[16] {
			// Swap bytes
			Unsafe.Add(ref b, 3), Unsafe.Add(ref b, 2), Unsafe.Add(ref b, 1), Unsafe.Add(ref b, 0),
			Unsafe.Add(ref b, 5), Unsafe.Add(ref b, 4),
			Unsafe.Add(ref b, 7), Unsafe.Add(ref b, 6),
			// Not swapped
			Unsafe.Add(ref b, 8), Unsafe.Add(ref b, 9),
			Unsafe.Add(ref b, 10), Unsafe.Add(ref b, 11), Unsafe.Add(ref b, 12), Unsafe.Add(ref b, 13), Unsafe.Add(ref b, 14), Unsafe.Add(ref b, 15),
		});
		#pragma warning restore format
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static byte[] ToUuidByteArray(in this Guid guid) {
		return GuidUuidSwap(guid.ToByteArray());
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool TryWriteUuidBytes(in this Guid guid, Span<byte> destination) {
		if (guid.TryWriteBytes(destination)) {
			GuidUuidSwap(destination);
			return true;
		}
		return false;
	}
}
