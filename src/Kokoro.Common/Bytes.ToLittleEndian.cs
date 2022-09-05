namespace Kokoro.Common;
using System.Buffers.Binary;

internal static partial class Bytes {

	// Exists only for completeness
	[MethodImpl(MethodImplOptions.AggressiveInlining)] public static sbyte ToLittleEndian(this sbyte value) => value;

	// Exists only for completeness
	[MethodImpl(MethodImplOptions.AggressiveInlining)] public static byte ToLittleEndian(this byte value) => value;

	// --

	[MethodImpl(MethodImplOptions.AggressiveInlining)] public static short ToLittleEndian(this short value) => BitConverter.IsLittleEndian ? value : BinaryPrimitives.ReverseEndianness(value);
	[MethodImpl(MethodImplOptions.AggressiveInlining)] public static int ToLittleEndian(this int value) => BitConverter.IsLittleEndian ? value : BinaryPrimitives.ReverseEndianness(value);
	[MethodImpl(MethodImplOptions.AggressiveInlining)] public static long ToLittleEndian(this long value) => BitConverter.IsLittleEndian ? value : BinaryPrimitives.ReverseEndianness(value);

	[MethodImpl(MethodImplOptions.AggressiveInlining)] public static ushort ToLittleEndian(this ushort value) => BitConverter.IsLittleEndian ? value : BinaryPrimitives.ReverseEndianness(value);
	[MethodImpl(MethodImplOptions.AggressiveInlining)] public static uint ToLittleEndian(this uint value) => BitConverter.IsLittleEndian ? value : BinaryPrimitives.ReverseEndianness(value);
	[MethodImpl(MethodImplOptions.AggressiveInlining)] public static ulong ToLittleEndian(this ulong value) => BitConverter.IsLittleEndian ? value : BinaryPrimitives.ReverseEndianness(value);
}
