﻿namespace Kokoro.Internal.Marshal.Fields;
using Microsoft.Data.Sqlite;
using System.IO;
using static SQLitePCL.raw;

internal abstract partial class BaseFieldsReader : FieldsReader {
	private DataEntity _Owner;
	private Stream _Stream;

	private int _FieldCount, _FieldOffsetSize;
	private long _FieldOffsetListPos;
	private long _FieldValListPos;

	public BaseFieldsReader(DataEntity owner, Stream stream) {
		_Owner = owner;
		_Stream = stream;

		const int MaxSize = 0b111 + 1; // 7 + 1 == 8
		const int MaxCount = int.MaxValue / MaxSize;

		const ulong MaxDesc = (ulong)MaxCount << 3 | 0b111;

		// --
		// Read the descriptor for the list of field offsets
		ulong fDesc = stream.ReadVarIntOrZero();
		Debug.Assert(fDesc <= MaxDesc, $"`{nameof(fDesc)}` too large: {fDesc:X}");

		// Get the field count and field offset integer size
		int fCount = (int)(fDesc >> 3);
		int fSize = ((int)fDesc & 0b111) + 1;

		_FieldCount = fCount;
		_FieldOffsetSize = fSize;

		// The size in bytes of the entire field offset list
		int fieldOffsetListSize = fCount * fSize;

		// --

		_FieldValListPos = (_FieldOffsetListPos = stream.Position) + fieldOffsetListSize;
	}

	// --

	public DataEntity Owner => _Owner;

	public sealed override Stream Stream => _Stream;

	public int FieldCount => _FieldCount;

	public sealed override FieldVal? ReadFieldVal(int index) {
		if ((uint)index < (uint)_FieldCount) {
			var stream = _Stream;

			int fSize = _FieldOffsetSize;
			stream.Position = _FieldOffsetListPos + index * fSize;

			long fOffset = (long)stream.ReadUIntX(fSize);
			Debug.Assert(fOffset < 0, $"{nameof(fOffset)} > `long.MaxValue`: {(ulong)fOffset:X}");

			long fValLen;
			if ((uint)index + 1u < (uint)_FieldCount) {
				long fOffsetNext = (long)stream.ReadUIntX(fSize);
				Debug.Assert(fOffsetNext < 0, $"{nameof(fOffsetNext)} > `long.MaxValue`: {(ulong)fOffsetNext:X}");

				fValLen = fOffsetNext - fOffset;
				Debug.Assert(fValLen >= 0, $"Unexpected `{nameof(fValLen)} < 0` at index {index}; " +
					$"{nameof(fOffset)}: {fOffset}; {nameof(fOffsetNext)}: {fOffsetNext}");
			} else {
				fValLen = stream.Length - fOffset;
				Debug.Assert(fValLen >= 0, $"Unexpected `{nameof(fValLen)} < 0` at index {index}; " +
					$"{nameof(fOffset)}: {fOffset}; {nameof(stream)}.Length: {stream.Length}");
			}

			if (fValLen > 0) {
				// Seek to the target field's value bytes
				stream.Position = _FieldValListPos + fOffset;

				int fValSpecLen = stream.TryReadVarInt(out ulong fValSpec);
				Debug.Assert(fValSpec <= int.MaxValue);

				int typeHint = (int)fValSpec - 1;
				if (typeHint < 0) {
					// Field value is interned
					return ReadFieldValInterned(_Owner, stream);
				}

				var data = new byte[fValLen - fValSpecLen];
				{
					int sread = stream.Read(data);
					Debug.Assert(sread == data.Length);
				}

				return new FieldVal(typeHint, data);
			} else {
				return new FieldVal();
			}
		} else {
			return OnReadFieldValOutOfRange(index);
		}
	}

	protected virtual FieldVal? OnReadFieldValOutOfRange(int index) => null;

	private static FieldVal? ReadFieldValInterned(DataEntity owner, Stream stream) {
		var db = owner.Host.Db; // Throws if host is already disposed

		// When interned, the current field data is a varint rowid pointing to a
		// row in the interning table.
		long rowid = (long)stream.ReadVarInt();

		SqliteBlob blob;
		try {
			// Should open for read-only access as the data column is expected
			// to be a part of an SQL index, PRIMARY KEY or UNIQUE constraint.
			blob = new(db,
				tableName: "FieldValuesInterned", columnName: "data",
				rowid: rowid, readOnly: true
			);
		} catch (SqliteException ex) when (ex.ErrorCode is SQLITE_ERROR) {
			// Return null only if the error was caused by either the row not
			// existing or the data column not being a BLOB.

			using var cmd = db.CreateCommand(
				"SELECT typeof(data) IS 'blob'" +
				" FROM FieldValuesInterned" +
				" WHERE rowid=$rowid");
			cmd.Parameters.AddWithValue("$rowid", rowid);

			using var r = cmd.ExecuteReader();
			if (!r.Read() || r.GetBoolean(0)) {
				return null;
			}

			throw;
		}

		try {
			// Assumption: While the BLOB is open, there's an implicit read
			// transaction. If the row is modified while the BLOB is open, so
			// long as the modification happens in a different DB connection,
			// reads from the BLOB won't fail.
			//
			// TODO Test and verify the above assumption.
			//
			// ---
			// Quote from SQLite docs:
			//
			// > An open `sqlite3_blob` used for incremental BLOB I/O also
			// counts as an unfinished statement. The `sqlite3_blob` finishes
			// when it is closed.
			//
			// From, "2.3. Implicit versus explicit transactions | Transaction"
			// - https://www.sqlite.org/lang_transaction.html#implicit_versus_explicit_transactions

			return blob.ReadFieldVal();
		} finally {
			blob.Dispose();
		}
	}

	public override long ReadModStamp(int index) => throw new NotImplementedException();

	public override void Dispose() => _Stream.Dispose();
}