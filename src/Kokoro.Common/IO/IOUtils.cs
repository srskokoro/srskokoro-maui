namespace Kokoro.Common.IO;

internal static partial class IOUtils {

	public static byte[] ToUTF8BytesWithBom(this string s) {
		var utf8 = Encoding.UTF8;
		var bom = utf8.Preamble;
		var bytes = new byte[bom.Length + utf8.GetByteCount(s)];
		utf8.GetBytes(s, bytes);
		return bytes;
	}

	public static byte[] ToUTF8Bytes(this string s) => Encoding.UTF8.GetBytes(s);
}
