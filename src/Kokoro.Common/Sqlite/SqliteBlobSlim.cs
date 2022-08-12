namespace Kokoro.Common.Sqlite;
using Microsoft.Data.Sqlite;
using SQLitePCL;
using System.Data;
using static SQLitePCL.raw;

/// <summary>
/// Provides access to the contents of a BLOB, similar to <see cref="SqliteBlob"/>,
/// but more lenient and optimized.
/// </summary>
/// <seealso href="https://docs.microsoft.com/dotnet/standard/data/sqlite/blob-io">BLOB I/O</seealso>
internal sealed class SqliteBlobSlim : Stream {
	private sqlite3_blob? _Blob;
	private readonly SqliteConnection _Connection;

	private long _Position;
	private readonly int _Length;
	private readonly bool _CanWrite;

	/// <summary>Wraps an existing <see cref="sqlite3_blob"/>.</summary>
	/// <param name="connection">
	/// An open connection to the database that owns the <paramref name="blob"/>.
	/// No validation is performed if this isn't the case. An exception might be
	/// thrown upon actual use.
	/// </param>
	/// <param name="blob">The SQLite 3 BLOB to wrap.</param>
	/// <param name="canWrite">
	/// Whether or not to support writing. If the underlying BLOB doesn't
	/// support writing, an exception will be thrown upon actual use.
	/// </param>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public SqliteBlobSlim(SqliteConnection connection, sqlite3_blob? blob, bool canWrite) {
		_Connection = connection;
		_Blob = blob;
		_Length = sqlite3_blob_bytes(blob);
		_CanWrite = canWrite;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="SqliteBlobSlim" /> class,
	/// or null if the underlying BLOB data cannot be accessed.
	/// </summary>
	/// <param name="connection">An open connection to the database.</param>
	/// <param name="databaseName">The name of the attached database containing the BLOB.</param>
	/// <param name="tableName">The name of table containing the BLOB.</param>
	/// <param name="columnName">The name of the column containing the BLOB.</param>
	/// <param name="rowid">The rowid of the row containing the BLOB.</param>
	/// <param name="canWrite">
	/// Whether or not to support writing. If <see langword="false"/>, the BLOB
	/// is opened for read-only access.
	/// </param>
	/// <param name="throwOnAccessFail">
	/// If <see langword="true"/>, throw instead of returning null on access
	/// failure.
	/// </param>
	/// <seealso href="https://docs.microsoft.com/dotnet/standard/data/sqlite/blob-io">BLOB I/O</seealso>
	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	[SkipLocalsInit]
	public static SqliteBlobSlim? Open(
		SqliteConnection connection,
		string databaseName,
		string tableName,
		string columnName,
		long rowid,
		bool canWrite,
		bool throwOnAccessFail = false
	) {
		if (connection == null) goto E_RequiresOpenConnection;

		sqlite3? connHandle = connection.Handle;
		if (connHandle == null) goto E_RequiresOpenConnection;

		DAssert_SqliteConnectionOpen(connection);

		int rc = sqlite3_blob_open(
			connHandle,
			databaseName,
			tableName,
			columnName,
			rowid,
			canWrite.ToByte(),
			out var blob
		);

		if (rc == SQLITE_OK) return new(connection, blob, canWrite);
		if (!throwOnAccessFail) return null;

		SqliteException.ThrowExceptionForRC(rc, connection.Handle);

	E_RequiresOpenConnection:
		return Open__E_RequiresOpenConnection();
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="SqliteBlobSlim" /> class,
	/// or null if the underlying BLOB data cannot be accessed.
	/// </summary>
	/// <param name="connection">An open connection to the database.</param>
	/// <param name="tableName">The name of table containing the BLOB.</param>
	/// <param name="columnName">The name of the column containing the BLOB.</param>
	/// <param name="rowid">The rowid of the row containing the BLOB.</param>
	/// <param name="canWrite">
	/// Whether or not to support writing. If <see langword="false"/>, the BLOB
	/// is opened for read-only access.
	/// </param>
	/// <param name="throwOnAccessFail">
	/// If <see langword="true"/>, throw instead of returning null on access
	/// failure.
	/// </param>
	/// <seealso href="https://docs.microsoft.com/dotnet/standard/data/sqlite/blob-io">BLOB I/O</seealso>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static SqliteBlobSlim? Open(
		SqliteConnection connection,
		string tableName,
		string columnName,
		long rowid,
		bool canWrite,
		bool throwOnAccessFail = false
	) => Open(
		connection,
		databaseName: "main",
		tableName,
		columnName,
		rowid,
		canWrite,
		throwOnAccessFail
	);

	// --

	public sqlite3_blob? Handle => _Blob;

	/// <summary>
	/// Gets a value indicating whether the current stream supports reading.
	/// Always true.
	/// </summary>
	/// <value><see langword="true" /> if the stream supports reading; otherwise, <see langword="false" />.</value>
	public override bool CanRead => true;

	/// <summary>
	/// Gets a value indicating whether the current stream supports writing.
	/// </summary>
	/// <value><see langword="true" /> if the stream supports writing; otherwise, <see langword="false" />.</value>
	public override bool CanWrite => _CanWrite;

	/// <summary>
	/// Gets a value indicating whether the current stream supports seeking.
	/// Always true.
	/// </summary>
	/// <value><see langword="true" /> if the stream supports seeking; otherwise, <see langword="false" />.</value>
	public override bool CanSeek => true;

	/// <summary>
	/// Gets the length in bytes of the stream.
	/// </summary>
	/// <value>A long value representing the length of the stream in bytes.</value>
	public override long Length => _Length;

	/// <summary>
	/// Gets or sets the position within the current stream.
	/// </summary>
	/// <value>The current position within the stream.</value>
	public override long Position {
		get => _Position;
		set => _Position = value;
	}

	/// <summary>
	/// Sets the position within the current stream.
	/// </summary>
	/// <param name="offset">
	/// A byte offset relative to the origin parameter.
	/// </param>
	/// <param name="origin">
	/// A value indicating the reference point used to obtain the new position.
	/// </param>
	/// <returns>The new position within the current stream.</returns>
	[SkipLocalsInit]
	public override long Seek(long offset, SeekOrigin origin) {
		long position;
		switch (origin) {
			case SeekOrigin.Begin: {
				position = offset;
				break;
			}
			case SeekOrigin.Current: {
				position = _Position + offset;
				break;
			}
			case SeekOrigin.End: {
				position = _Length + offset;
				break;
			}
			default: {
				return Seek__E_InvalidOrigin_Arg(origin);
			}
		}
		return _Position = position;
	}

	/// <summary>
	/// Reads a sequence of bytes from the current stream and advances the
	/// position within the stream by the number of bytes read.
	/// </summary>
	/// <param name="buffer">
	/// An array of bytes. When this method returns, the buffer contains the
	/// specified byte array with the values between <paramref name="offset"/>
	/// and (<paramref name="offset"/> + <paramref name="count"/> - 1) replaced
	/// by the bytes read from the current source.
	/// </param>
	/// <param name="offset">
	/// The zero-based byte offset in buffer at which to begin storing the data
	/// read from the current stream.
	/// </param>
	/// <param name="count">
	/// The maximum number of bytes to be read from the current stream.
	/// </param>
	/// <returns>The total number of bytes read into the buffer.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public override int Read(byte[] buffer, int offset, int count)
		=> Read(buffer.AsSpan(offset, count));

	/// <summary>
	/// Reads a sequence of bytes from the current stream and advances the
	/// position within the stream by the number of bytes read.
	/// </summary>
	/// <param name="buffer">
	/// A region of memory. When this method returns, the contents of this
	/// region are replaced by the bytes read from the current source.
	/// </param>
	/// <returns>
	/// The total number of bytes read into the buffer. This can be less than
	/// the number of bytes allocated in the buffer if that many bytes are not
	/// currently available, or zero (0) if the end of the stream has been
	/// reached.
	/// </returns>
	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	[SkipLocalsInit]
	public override int Read(Span<byte> buffer) {
		long position = _Position;
		int count = buffer.Length;
		int length;
		if ((ulong)(position + count) > (uint)(length = _Length)) {
			if ((ulong)position <= (uint)length) {
				count = length - (int)position;
			} else if (position >= 0) {
				position = length;
				count = 0;
			} else {
				Read__E_BeforeBegin_IO();
			}
		}

		int rc = sqlite3_blob_read(_Blob, buffer[..count], (int)position);
		if (rc == SQLITE_OK) {
			_Position += count;
			return count;
		} else {
			SqliteException.ThrowExceptionForRC(rc, _Connection.Handle);
			return count;
		}
	}

	/// <summary>
	/// Writes a sequence of bytes to the current stream and advances the
	/// current position within this stream by the number of bytes written.
	/// </summary>
	/// <param name="buffer">
	/// An array of bytes. This method copies count bytes from buffer to the
	/// current stream.
	/// </param>
	/// <param name="offset">
	/// The zero-based byte offset in buffer at which to begin copying bytes to
	/// the current stream.
	/// </param>
	/// <param name="count">
	/// The number of bytes to be written to the current stream.
	/// </param>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public override void Write(byte[] buffer, int offset, int count)
		=> Write(buffer.AsSpan(offset, count));

	/// <summary>
	/// Writes a sequence of bytes to the current stream and advances the
	/// current position within this stream by the number of bytes written.
	/// </summary>
	/// <param name="buffer">
	/// A region of memory. This method copies the contents of this region to
	/// the current stream.
	/// </param>
	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	[SkipLocalsInit]
	public override void Write(ReadOnlySpan<byte> buffer) {
		long position = _Position;
		int count = buffer.Length;
		int length;
		if ((ulong)(position + count) > (uint)(length = _Length)) {
			if ((ulong)position > (uint)length) {
				if (position >= 0) {
					position = length;
				} else {
					Write__E_BeforeBegin_IO();
				}
			}
			if (count != 0) {
				SetLength__E_ResizeNotSupported_NS();
			}
		}

		int rc = sqlite3_blob_write(_Blob, buffer[..count], (int)position);
		if (rc == SQLITE_OK) {
			_Position += count;
			return; // Early exit
		}

		if (rc == SQLITE_READONLY) {
			Write__E_ReadOnly_NS();
		}
		SqliteException.ThrowExceptionForRC(rc, _Connection.Handle);
	}

	/// <summary>
	/// Releases any resources used by the blob and closes it.
	/// </summary>
	/// <param name="disposing">
	/// true to release managed and unmanaged resources; <see langword="false" />
	/// to release only unmanaged resources.
	/// </param>
	protected override void Dispose(bool disposing) {
		if (_Blob != null) {
			_Blob.Dispose();
			_Blob = null;
		}
	}

	/// <summary>
	/// Clears all buffers for this stream and causes any buffered data to be
	/// written to the underlying device. Does nothing.
	/// </summary>
	public override void Flush() { }

	/// <summary>
	/// Sets the length of the current stream. This is not supported by SQLite 3
	/// BLOBs.
	/// </summary>
	/// <param name="value">The desired length of the current stream in bytes.</param>
	/// <exception cref="NotSupportedException">Always.</exception>
	public override void SetLength(long value) => SetLength__E_ResizeNotSupported_NS();

	// --

	[Conditional("DEBUG")]
	private static void DAssert_SqliteConnectionOpen(SqliteConnection connection) {
		var state = connection.State;
		Debug.Assert(state == ConnectionState.Open
			, $"`{nameof(SqliteConnection)}` expected to be open at this point but was `{state}`");
	}

	[DoesNotReturn]
	private static SqliteBlobSlim Open__E_RequiresOpenConnection()
		=> throw new InvalidOperationException($"`{nameof(SqliteBlobSlim)}` can only be used when the connection is open.");

	[DoesNotReturn]
	private static void SetLength__E_ResizeNotSupported_NS()
		=> throw new NotSupportedException($"The size of a BLOB may not be changed by the `{nameof(SqliteBlobSlim)}` API. Use an UPDATE command instead.");

	[DoesNotReturn]
	private static long Seek__E_InvalidOrigin_Arg(SeekOrigin origin)
		=> throw new ArgumentException($"The `{typeof(SeekOrigin)}` enumeration value `{origin}` is invalid.", nameof(origin));

	[DoesNotReturn]
	private static void Read__E_BeforeBegin_IO()
		=> throw new IOException("An attempt was made to read from a position before the beginning of the stream.");

	[DoesNotReturn]
	private static void Write__E_BeforeBegin_IO()
		=> throw new IOException("An attempt was made to write to a position before the beginning of the stream.");

	[DoesNotReturn]
	private static void Write__E_ReadOnly_NS()
		=> throw new NotSupportedException("Stream does not support writing.");
}
