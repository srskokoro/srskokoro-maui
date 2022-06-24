namespace Kokoro.Common.Text;

internal static partial class TextUtils {

	internal static class EncodingCache {
		internal static readonly UTF8Encoding UTF8N = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false);
		internal static readonly UTF32Encoding UTF32BE = new(bigEndian: true, byteOrderMark: true);
	}

	/// <summary>UTF-8-NOBOM</summary>
	public static Encoding UTF8N => EncodingCache.UTF8N;

	/// <summary>
	/// The maximum number of bytes that <see cref="GetEncoding(Span{byte})"/>
	/// would interpret as the byte order mark (BOM).
	/// </summary>
	internal const int MaxBytesForBom = 4;

	/// <summary>
	/// The maximum number of bytes that an <see cref="Encoding"/> returned by
	/// <see cref="GetEncoding(Span{byte})"/> would use to represent a
	/// character.
	/// </summary>
	internal const int MaxBytesPerChar = 4;

	/// <summary>
	/// Determines the encoding by analyzing the byte order mark (BOM).
	/// Defaults to <see cref="UTF8N">UTF-8-NOBOM</see> when detection fails.
	/// </summary>
	/// <remarks>
	/// This is meant as a predictable alternative to <see cref="StreamReader.CurrentEncoding"/>
	/// </remarks>
	/// <param name="bytes">The span of bytes to analyze.</param>
	/// <returns>The detected encoding.</returns>
	public static Encoding GetEncoding(Span<byte> bytes) {
		/// NOTE: If this algorithm is changed to support more encodings, make
		/// sure to adjust <see cref="MaxBytesPerChar"/> and <see cref="MaxBytesForBom"/>
		/// as appropriate.
		if (bytes.Length >= 2) {
			if (bytes[0] > 0x7f) {
				// Optimized for the common case: UTF-8-BOM
				if (bytes.Length >= 3 && bytes[0] == 0xef && bytes[1] == 0xbb && bytes[2] == 0xbf) {
					return Encoding.UTF8; // UTF-8-BOM
				}
				if (bytes[0] == 0xfe && bytes[1] == 0xff) {
					return Encoding.BigEndianUnicode; // UTF-16BE
				}
				if (bytes[0] == 0xff && bytes[1] == 0xfe) {
					if (bytes.Length >= 4 && bytes[2] == 0 && bytes[3] == 0) {
						return Encoding.UTF32; // UTF-32LE
					}
					return Encoding.Unicode; // UTF-16LE
				}
			} else if (bytes.Length >= 4 && bytes[0] == 0 && bytes[1] == 0 && bytes[2] == 0xfe && bytes[3] == 0xff) {
				return EncodingCache.UTF32BE; // UTF-32BE
			}
		}
		return UTF8N; // UTF-8-NOBOM
	}
}
