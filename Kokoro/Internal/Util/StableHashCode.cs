namespace Kokoro.Internal.Util;
using System.Runtime.InteropServices;

internal static class StableHashCode {

	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	public static int Of(ReadOnlySpan<char> chars) {
		// Modified from, https://stackoverflow.com/a/36846609
		unchecked {
			// TODO Use hardware accelerated vector instructions instead
			// See, https://github.com/CommunityToolkit/dotnet/blob/7daf7bf7ce228c48a818dc7e833973f6323b598d/CommunityToolkit.HighPerformance/Helpers/Internals/SpanHelper.Hash.cs#L82
			// See also, https://sergiopedri.medium.com/186816010ad9

			int hash1 = 5381;
			int hash2 = hash1;

			for (int i = 0; ;) {
				if (i >= chars.Length) break;
				hash1 = ((hash1 << 5) + hash1) ^ chars[i++];

				if (i >= chars.Length) break;
				hash2 = ((hash2 << 5) + hash2) ^ chars[i++];
			}

			hash1 += hash2 * 1566083941;
			return (hash1 << 5) - hash1 + (chars.Length << 1);
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	public static int Of(ReadOnlySpan<byte> bytes) {
		// Modified from, https://stackoverflow.com/a/36846609
		unchecked {
			// TODO Use hardware accelerated vector instructions instead
			// See, https://github.com/CommunityToolkit/dotnet/blob/7daf7bf7ce228c48a818dc7e833973f6323b598d/CommunityToolkit.HighPerformance/Helpers/Internals/SpanHelper.Hash.cs#L82
			// See also, https://sergiopedri.medium.com/186816010ad9

			int hash1 = 5381;
			int hash2 = hash1;

			for (int i = 0; ;) {
				if (i >= bytes.Length) break;
				hash1 = ((hash1 << 5) + hash1) ^ (bytes[i++] << 8);
				if (i >= bytes.Length) break;
				hash1 ^= bytes[i++];

				if (i >= bytes.Length) break;
				hash2 = ((hash2 << 5) + hash2) ^ (bytes[i++] << 8);
				if (i >= bytes.Length) break;
				hash2 ^= bytes[i++];
			}

			hash1 += hash2 * 1566083941;
			return (hash1 << 5) - hash1 + bytes.Length;
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static int Of<T>(in T unmanaged) where T : unmanaged {
		return Of(MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<T, byte>(ref Unsafe.AsRef(in unmanaged)), Unsafe.SizeOf<T>()));
	}
}
