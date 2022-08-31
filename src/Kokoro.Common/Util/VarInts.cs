namespace Kokoro.Common.Util;
using System.Runtime.InteropServices;

// Variable-length integer encoding from SQLite 4.
//
// See,
// - https://sqlite.org/src4/doc/trunk/www/varint.wiki
// - https://sqlite.org/src4/file?name=src/varint.c&ci=trunk
//
internal static class VarInts {

	public const int MaxLength = MaxLength64;
	public const int MaxLength64 = 9;
	public const int MaxLength32 = 5;

	public const int LengthForZero = LengthFor0xF0OrLess;
	public const int LengthFor0xF0OrLess = 1;

	[MethodImpl(MethodImplOptions.AggressiveOptimization)] // Use fully optimizing JIT, right from the start!
	[SkipLocalsInit]
	public static int Read(ReadOnlySpan<byte> src, out ulong result) {
		int n = src.Length;
		if (n < 1) goto Fail;

		ref byte b = ref MemoryMarshal.GetReference(src);

		if (b <= 240) {
			result = b;
			return 1;
		}
		if (b <= 248) {
			if (n < 2) goto Fail;
			result = ((b - 241u) << 8) + U.Add(ref b, 1) + 240u;
			return 2;
		}

		if (n < b - 246) goto Fail;

		if (b == 249) {
			result = ((uint)U.Add(ref b, 1) << 8) + U.Add(ref b, 2) + 2288u;
			return 3;
		}
		if (b == 250) {
			result = ((uint)U.Add(ref b, 1) << 16) + ((uint)U.Add(ref b, 2) << 8) + U.Add(ref b, 3);
			return 4;
		}

		uint x = ((uint)U.Add(ref b, 1) << 24) + ((uint)U.Add(ref b, 2) << 16) + ((uint)U.Add(ref b, 3) << 8) + U.Add(ref b, 4);

		if (b == 251) {
			result = x;
			return 5;
		}
		if (b == 252) {
			result = (((ulong)x) << 8) + U.Add(ref b, 5);
			return 6;
		}
		if (b == 253) {
			result = (((ulong)x) << 16) + (((uint)U.Add(ref b, 5) << 8) + U.Add(ref b, 6));
			return 7;
		}
		if (b == 254) {
			result = (((ulong)x) << 24) + (((uint)U.Add(ref b, 5) << 16) + ((uint)U.Add(ref b, 6) << 8) + U.Add(ref b, 7));
			return 8;
		}

		result = (((ulong)x) << 32) + (((uint)U.Add(ref b, 5) << 24) + ((uint)U.Add(ref b, 6) << 16) + ((uint)U.Add(ref b, 7) << 8) + U.Add(ref b, 8));
		return 9;

	Fail:
		result = 0;
		return 0;
	}

	[MethodImpl(MethodImplOptions.AggressiveOptimization)] // Use fully optimizing JIT, right from the start!
	[SkipLocalsInit]
	private static void Write32BitsInternal(ref byte b, uint v) {
		U.Add(ref b, 0) = (byte)(v >> 24);
		U.Add(ref b, 1) = (byte)(v >> 16);
		U.Add(ref b, 2) = (byte)(v >> 8);
		U.Add(ref b, 3) = (byte)v;
	}

	[MethodImpl(MethodImplOptions.AggressiveOptimization)] // Use fully optimizing JIT, right from the start!
	[SkipLocalsInit]
	public static int Write(Span<byte> dest, ulong value) {
		// Initial check forces JIT to avoid unnecessary range checking
		if (dest.Length < 9) goto Fail;

		if (value <= 240) {
			dest[0] = (byte)value;
			return 1;
		}
		if (value <= 2287) {
			uint v = (uint)value - 240;
			dest[0] = (byte)((v >> 8) + 241);
			dest[1] = (byte)v;
			return 2;
		}
		if (value <= 67823) {
			dest[0] = 249;
			uint v = (uint)value - 2288;
			dest[1] = (byte)(v >> 8);
			dest[2] = (byte)v;
			return 3;
		}

		uint l = (uint)value;
		uint h = (uint)(value >> 32);
		if (h == 0) {
			if (l <= 0xFF_FFFF) {
				dest[0] = 250;
				dest[1] = (byte)(l >> 16);
				dest[2] = (byte)(l >> 8);
				dest[3] = (byte)l;
				return 4;
			}
			dest[0] = 251;
			Write32BitsInternal(ref dest[1], l);
			return 5;
		}
		if (h <= 0xFF) {
			dest[0] = 252;
			dest[1] = (byte)h;
			Write32BitsInternal(ref dest[2], l);
			return 6;
		}
		if (h <= 0xFFFF) {
			dest[0] = 253;
			dest[1] = (byte)(h >> 8);
			dest[2] = (byte)h;
			Write32BitsInternal(ref dest[3], l);
			return 7;
		}
		if (h <= 0xFF_FFFF) {
			dest[0] = 254;
			dest[1] = (byte)(h >> 16);
			dest[2] = (byte)(h >> 8);
			dest[3] = (byte)h;
			Write32BitsInternal(ref dest[4], l);
			return 8;
		}
		dest[0] = 255;
		Write32BitsInternal(ref dest[1], h);
		Write32BitsInternal(ref dest[5], l);
		return 9;

	Fail:
		E_DestinationTooShort_AOOR();
		return 0;

		[DoesNotReturn]
		static void E_DestinationTooShort_AOOR() => throw new ArgumentOutOfRangeException(nameof(dest),
			"Destination span must be at least 9 bytes to accommodate the largest possible varint.");
	}

	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	[SkipLocalsInit]
	public static int Length(ulong value) {
		// Favor small values (as they're more common) more than larger values

		if (value <= 240) return 1;
		if (value <= 2287) return 2;
		if (value <= 67823) return 3;

		uint l = (uint)value;
		uint h = (uint)(value >> 32);
		if (h == 0) {
			if (l <= 0xFF_FFFF) {
				return 4;
			}
			return 5;
		}

		if (h <= 0xFF) return 6;
		if (h <= 0xFFFF) return 7;
		if (h <= 0xFF_FFFF) return 8;

		return 9;
	}

	[MethodImpl(MethodImplOptions.AggressiveOptimization)] // Use fully optimizing JIT, right from the start!
	[SkipLocalsInit]
	public static int Write(Span<byte> dest, uint value) {
		// Initial check forces JIT to avoid unnecessary range checking
		if (dest.Length < 5) goto Fail;

		if (value <= 240) {
			dest[0] = (byte)value;
			return 1;
		}
		if (value <= 2287) {
			value -= 240;
			dest[0] = (byte)((value >> 8) + 241);
			dest[1] = (byte)value;
			return 2;
		}
		if (value <= 67823) {
			dest[0] = 249;
			value -= 2288;
			dest[1] = (byte)(value >> 8);
			dest[2] = (byte)value;
			return 3;
		}

		if (value <= 0xFF_FFFF) {
			dest[0] = 250;
			dest[1] = (byte)(value >> 16);
			dest[2] = (byte)(value >> 8);
			dest[3] = (byte)value;
			return 4;
		}
		dest[0] = 251;
		Write32BitsInternal(ref dest[1], value);
		return 5;

	Fail:
		E_DestinationTooShort_AOOR();
		return 0;

		[DoesNotReturn]
		static void E_DestinationTooShort_AOOR() => throw new ArgumentOutOfRangeException(nameof(dest),
			"Destination span must be at least 5 bytes to accommodate the largest possible varint.");
	}

	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	[SkipLocalsInit]
	public static int Length(uint value) {
		// Favor small values (as they're more common) more than larger values
		if (value <= 240) return 1;
		if (value <= 2287) return 2;
		if (value <= 67823) return 3;
		if (value <= 0xFF_FFFF) return 4;
		return 5;
	}

	// --

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[SkipLocalsInit]
	public static byte[] Bytes(ulong value) {
		Span<byte> buffer = stackalloc byte[MaxLength64];
		int len = Write(buffer, value);
		return buffer.Slice(0, len).ToArray();
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[SkipLocalsInit]
	public static byte[] Bytes(uint value) {
		Span<byte> buffer = stackalloc byte[MaxLength32];
		int len = Write(buffer, value);
		return buffer.Slice(0, len).ToArray();
	}
}
