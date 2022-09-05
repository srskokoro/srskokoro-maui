namespace Kokoro.Common.Text;
using System.Runtime.InteropServices;

internal static partial class TextUtils {

	public static byte[] ToUTF8BytesWithBom(this string s) {
		var utf8 = Encoding.UTF8;

		int count = utf8.GetByteCount(s);
		var bytes = new byte[3 + count];

		ref byte r0 = ref bytes.DangerousGetReference();
		r0 = 0xef; U.Add(ref r0, 1) = 0xbb; U.Add(ref r0, 2) = 0xbf;
		Debug.Assert(utf8.Preamble.SequenceEqual(MemoryMarshal.CreateSpan(ref r0, 3)));

		utf8.GetBytes(s, MemoryMarshal.CreateSpan(ref U.Add(ref r0, 3), count));
		return bytes;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static byte[] ToUTF8Bytes(this string s) => Encoding.UTF8.GetBytes(s);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Span<byte> ToUTF8Bytes(this string s, Span<byte> bytes) => bytes.Slice(0, s.GetUTF8Bytes(bytes));

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static int GetUTF8Bytes(this string s, Span<byte> bytes) => Encoding.UTF8.GetBytes(s, bytes);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static int GetUTF8ByteCount(this string s) => Encoding.UTF8.GetByteCount(s);

	// --

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static string UTF8ToString(Span<byte> bytes) => Encoding.UTF8.GetString(bytes);
}
