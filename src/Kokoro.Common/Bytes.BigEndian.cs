namespace Kokoro.Common;
using System.Buffers.Binary;

internal static partial class Bytes {

	// Exists only for completeness
	[MethodImpl(MethodImplOptions.AggressiveInlining)] public static sbyte BigEndian(this sbyte value) => value;

	// Exists only for completeness
	[MethodImpl(MethodImplOptions.AggressiveInlining)] public static byte BigEndian(this byte value) => value;

	// --

	[MethodImpl(MethodImplOptions.AggressiveInlining)] public static short BigEndian(this short value) => BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
	[MethodImpl(MethodImplOptions.AggressiveInlining)] public static int BigEndian(this int value) => BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
	[MethodImpl(MethodImplOptions.AggressiveInlining)] public static long BigEndian(this long value) => BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;

	[MethodImpl(MethodImplOptions.AggressiveInlining)] public static ushort BigEndian(this ushort value) => BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
	[MethodImpl(MethodImplOptions.AggressiveInlining)] public static uint BigEndian(this uint value) => BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
	[MethodImpl(MethodImplOptions.AggressiveInlining)] public static ulong BigEndian(this ulong value) => BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
}
