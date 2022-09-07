namespace Kokoro.Common;
using System.Buffers.Binary;

// Utilities to toggle primitive types to and from little-endian.
internal static partial class Bytes {

	// Exists only for completeness
	[MethodImpl(MethodImplOptions.AggressiveInlining)] public static sbyte LittleEndian(this sbyte value) => value;

	// Exists only for completeness
	[MethodImpl(MethodImplOptions.AggressiveInlining)] public static byte LittleEndian(this byte value) => value;

	// --

	[MethodImpl(MethodImplOptions.AggressiveInlining)] public static short LittleEndian(this short value) => !BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
	[MethodImpl(MethodImplOptions.AggressiveInlining)] public static int LittleEndian(this int value) => !BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
	[MethodImpl(MethodImplOptions.AggressiveInlining)] public static long LittleEndian(this long value) => !BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;

	[MethodImpl(MethodImplOptions.AggressiveInlining)] public static ushort LittleEndian(this ushort value) => !BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
	[MethodImpl(MethodImplOptions.AggressiveInlining)] public static uint LittleEndian(this uint value) => !BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
	[MethodImpl(MethodImplOptions.AggressiveInlining)] public static ulong LittleEndian(this ulong value) => !BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
}
