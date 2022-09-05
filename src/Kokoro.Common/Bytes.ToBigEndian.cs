namespace Kokoro.Common;
using System.Buffers.Binary;

internal static partial class Bytes {

	// Exists only for completeness
	[MethodImpl(MethodImplOptions.AggressiveInlining)] public static sbyte ToBigEndian(this sbyte value) => value;

	// Exists only for completeness
	[MethodImpl(MethodImplOptions.AggressiveInlining)] public static byte ToBigEndian(this byte value) => value;

	// --

	[MethodImpl(MethodImplOptions.AggressiveInlining)] public static short ToBigEndian(this short value) => BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
	[MethodImpl(MethodImplOptions.AggressiveInlining)] public static int ToBigEndian(this int value) => BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
	[MethodImpl(MethodImplOptions.AggressiveInlining)] public static long ToBigEndian(this long value) => BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;

	[MethodImpl(MethodImplOptions.AggressiveInlining)] public static ushort ToBigEndian(this ushort value) => BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
	[MethodImpl(MethodImplOptions.AggressiveInlining)] public static uint ToBigEndian(this uint value) => BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
	[MethodImpl(MethodImplOptions.AggressiveInlining)] public static ulong ToBigEndian(this ulong value) => BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
}
