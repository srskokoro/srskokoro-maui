namespace Kokoro.Common.Text;

internal static partial class TextUtils {

	public static byte[] ToUTF8BytesWithBom(this string s) {
		var utf8 = Encoding.UTF8;
		var bom = utf8.Preamble;
		var bytes = new byte[bom.Length + utf8.GetByteCount(s)];
		utf8.GetBytes(s, bytes);
		return bytes;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static byte[] ToUTF8Bytes(this string s) => Encoding.UTF8.GetBytes(s);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Span<byte> ToUTF8Bytes(this string s, Span<byte> bytes) => bytes[..s.GetUTF8Bytes(bytes)];

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static int GetUTF8Bytes(this string s, Span<byte> bytes) => Encoding.UTF8.GetBytes(s, bytes);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static int GetUTF8ByteCount(this string s) => Encoding.UTF8.GetByteCount(s);
}
