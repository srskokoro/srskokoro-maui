namespace Kokoro.Internal.Sqlite.Extensions;
using Microsoft.Data.Sqlite;

internal static class SqliteDataReaderExtensions {

	public static UniqueId GetUniqueId(this SqliteDataReader reader, int ordinal)
		=> new(reader.GetBytes(ordinal));


	public static byte[] GetBytes(this SqliteDataReader reader, int ordinal) {
		if (reader.GetValue(ordinal) is byte[] bytes) {
			return bytes;
		}
		return E_CalledOnNullValue_InvOp<byte[]>(ordinal);
	}

	public static byte[]? GetBytesOrNull(this SqliteDataReader reader, int ordinal) {
		if (reader.GetValue(ordinal) is byte[] bytes) {
			return bytes;
		}
		return null;
	}

	public static byte[] GetBytesOrEmpty(this SqliteDataReader reader, int ordinal) {
		if (reader.GetValue(ordinal) is byte[] bytes) {
			return bytes;
		}
		return Array.Empty<byte>();
	}


	[DoesNotReturn]
	private static T E_CalledOnNullValue_InvOp<T>(int ordinal) {
		// From, https://github.com/dotnet/efcore/blob/v6.0.3/src/Microsoft.Data.Sqlite.Core/Properties/Resources.resx#L193
		throw new InvalidOperationException($"The data is NULL at ordinal {ordinal}. This method can't be called on NULL values. Check using IsDBNull before calling.");
	}
}
