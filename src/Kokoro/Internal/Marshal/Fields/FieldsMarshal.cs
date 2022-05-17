namespace Kokoro.Internal.Marshal.Fields;

internal abstract class FieldsMarshal : IDisposable {
	private protected Stream _Stream;

	private protected int _FieldCount, _FieldOffsetSize;
	private protected long _FieldOffsetListPos;
	private protected long _FieldValListPos;


	public Stream Stream => _Stream;

	public int FieldCount => _FieldCount;


#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor.
	private protected FieldsMarshal() { }
#pragma warning restore CS8618

	public FieldsMarshal(Stream stream) {
		_Stream = stream;

		const int MaxSize = 0b111 + 1; // 7 + 1 == 8
		const int MaxCount = int.MaxValue / MaxSize;

		const ulong MaxDesc = (ulong)MaxCount << 3 | 0b111;

		// --
		// Read the descriptor for the list of field offsets
		ulong fDesc = stream.ReadVarInt();
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


	public virtual void Dispose() => _Stream.Dispose();

	public FieldVal? ReadFieldVal(int index) {
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

				ulong fValSpec = stream.ReadVarInt();
				Debug.Assert(fValSpec <= int.MaxValue);

				int typeHint = (int)fValSpec - 1;
				if (typeHint < 0) {
					// Field value is interned

					// TODO-XXX Handle interned field value
					// - When interned, the current field data is a varint rowid
					// pointing to a row in an interning table.
					// - It shouldn't be possible to load the current data
					// without first resolving to the actual data interned.
				}

				var data = new byte[fValLen];
				{
					int sread = stream.Read(data);
					Debug.Assert(sread == fValLen);
				}

				return new FieldVal(typeHint, data);
			} else {
				return new FieldVal(FieldTypeHint.Null, Array.Empty<byte>());
			}
		} else {
			return OnReadFieldValFail(index);
		}
	}

	protected virtual FieldVal? OnReadFieldValFail(int index) => null;
}
