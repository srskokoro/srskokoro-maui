namespace Kokoro.Common.Blake2Fast.Extensions;
using global::Blake2Fast.Implementation;
using Kokoro.Common.Util;
using System.Buffers.Binary;

internal static partial class Blake2bHashStateExtensions {

	public static void UpdateWithVarInt(this Blake2bHashState state, uint input) {
		Span<byte> buffer = stackalloc byte[5];
		VarInts.Write(buffer, input);
		state.Update(buffer);
	}

	public static void UpdateWithVarInt(this Blake2bHashState state, ulong input) {
		Span<byte> buffer = stackalloc byte[9];
		VarInts.Write(buffer, input);
		state.Update(buffer);
	}
}

// Little-Endian
static partial class Blake2bHashStateExtensions {

	// Exists only for completeness
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void UpdateLE(this Blake2bHashState state, sbyte input) => state.Update(input);

	// Exists only for completeness
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void UpdateLE(this Blake2bHashState state, byte input) => state.Update(input);

	// --

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void UpdateLE(this Blake2bHashState state, short input) {
		if (!BitConverter.IsLittleEndian) {
			short tmp = BinaryPrimitives.ReverseEndianness(input);
			state.Update(tmp);
		} else {
			state.Update(input);
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void UpdateLE(this Blake2bHashState state, int input) {
		if (!BitConverter.IsLittleEndian) {
			int tmp = BinaryPrimitives.ReverseEndianness(input);
			state.Update(tmp);
		} else {
			state.Update(input);
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void UpdateLE(this Blake2bHashState state, long input) {
		if (!BitConverter.IsLittleEndian) {
			long tmp = BinaryPrimitives.ReverseEndianness(input);
			state.Update(tmp);
		} else {
			state.Update(input);
		}
	}

	// --

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void UpdateLE(this Blake2bHashState state, ushort input) {
		if (!BitConverter.IsLittleEndian) {
			ushort tmp = BinaryPrimitives.ReverseEndianness(input);
			state.Update(tmp);
		} else {
			state.Update(input);
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void UpdateLE(this Blake2bHashState state, uint input) {
		if (!BitConverter.IsLittleEndian) {
			uint tmp = BinaryPrimitives.ReverseEndianness(input);
			state.Update(tmp);
		} else {
			state.Update(input);
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void UpdateLE(this Blake2bHashState state, ulong input) {
		if (!BitConverter.IsLittleEndian) {
			ulong tmp = BinaryPrimitives.ReverseEndianness(input);
			state.Update(tmp);
		} else {
			state.Update(input);
		}
	}

	// --

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void UpdateLE(this Blake2bHashState state, Half input) {
		if (!BitConverter.IsLittleEndian) {
			ushort tmp = BinaryPrimitives.ReverseEndianness(BitConverter.HalfToUInt16Bits(input));
			state.Update(tmp);
		} else {
			state.Update(input);
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void UpdateLE(this Blake2bHashState state, float input) {
		if (!BitConverter.IsLittleEndian) {
			uint tmp = BinaryPrimitives.ReverseEndianness(BitConverter.SingleToUInt32Bits(input));
			state.Update(tmp);
		} else {
			state.Update(input);
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void UpdateLE(this Blake2bHashState state, double input) {
		if (!BitConverter.IsLittleEndian) {
			ulong tmp = BinaryPrimitives.ReverseEndianness(BitConverter.DoubleToUInt64Bits(input));
			state.Update(tmp);
		} else {
			state.Update(input);
		}
	}
}

// Big-Endian
static partial class Blake2bHashStateExtensions {

	// Exists only for completeness
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void UpdateBE(this Blake2bHashState state, sbyte input) => state.Update(input);

	// Exists only for completeness
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void UpdateBE(this Blake2bHashState state, byte input) => state.Update(input);

	// --

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void UpdateBE(this Blake2bHashState state, short input) {
		if (BitConverter.IsLittleEndian) {
			short tmp = BinaryPrimitives.ReverseEndianness(input);
			state.Update(tmp);
		} else {
			state.Update(input);
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void UpdateBE(this Blake2bHashState state, int input) {
		if (BitConverter.IsLittleEndian) {
			int tmp = BinaryPrimitives.ReverseEndianness(input);
			state.Update(tmp);
		} else {
			state.Update(input);
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void UpdateBE(this Blake2bHashState state, long input) {
		if (BitConverter.IsLittleEndian) {
			long tmp = BinaryPrimitives.ReverseEndianness(input);
			state.Update(tmp);
		} else {
			state.Update(input);
		}
	}

	// --

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void UpdateBE(this Blake2bHashState state, ushort input) {
		if (BitConverter.IsLittleEndian) {
			ushort tmp = BinaryPrimitives.ReverseEndianness(input);
			state.Update(tmp);
		} else {
			state.Update(input);
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void UpdateBE(this Blake2bHashState state, uint input) {
		if (BitConverter.IsLittleEndian) {
			uint tmp = BinaryPrimitives.ReverseEndianness(input);
			state.Update(tmp);
		} else {
			state.Update(input);
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void UpdateBE(this Blake2bHashState state, ulong input) {
		if (BitConverter.IsLittleEndian) {
			ulong tmp = BinaryPrimitives.ReverseEndianness(input);
			state.Update(tmp);
		} else {
			state.Update(input);
		}
	}

	// --

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void UpdateBE(this Blake2bHashState state, Half input) {
		if (BitConverter.IsLittleEndian) {
			ushort tmp = BinaryPrimitives.ReverseEndianness(BitConverter.HalfToUInt16Bits(input));
			state.Update(tmp);
		} else {
			state.Update(input);
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void UpdateBE(this Blake2bHashState state, float input) {
		if (BitConverter.IsLittleEndian) {
			uint tmp = BinaryPrimitives.ReverseEndianness(BitConverter.SingleToUInt32Bits(input));
			state.Update(tmp);
		} else {
			state.Update(input);
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void UpdateBE(this Blake2bHashState state, double input) {
		if (BitConverter.IsLittleEndian) {
			ulong tmp = BinaryPrimitives.ReverseEndianness(BitConverter.DoubleToUInt64Bits(input));
			state.Update(tmp);
		} else {
			state.Update(input);
		}
	}
}
