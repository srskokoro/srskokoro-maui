namespace Kokoro.Internal.Marshal.Fields;
using System.IO;

internal abstract class BaseFieldsReader<TOwner> : FieldsReader
		where TOwner : DataEntity {

	private readonly TOwner _Owner;
	private readonly Stream _Stream;

	private readonly int _FieldCount, _FieldOffsetSize;
	private readonly long _FieldOffsetListPos;
	private readonly long _FieldValListPos;

	public BaseFieldsReader(TOwner owner, Stream stream) {
		_Owner = owner;
		_Stream = stream;

		try {
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

		} catch (NotSupportedException) when (!stream.CanRead || !stream.CanSeek) {
			// NOTE: We ensure that the constructor never throws under normal
			// circumstances: if we simply couldn't read the needed data to
			// complete initialization, then we should just swallow the
			// exception, and initialize with reasonable defaults. So far, the
			// only exception we should guard against is the one thrown when the
			// stream doesn't support reading or seeking (as the code in the
			// above try-block requires it).
			//
			// That way, a try-finally or `using` block can be set up right
			// after construction, to properly dispose the state along with the
			// stream, all in one go.

			_FieldCount = 0;
			_FieldOffsetSize = 1;
			_FieldValListPos = _FieldOffsetListPos = 0;
		}
	}

	// --

	public TOwner Owner => _Owner;

	public sealed override Stream Stream => _Stream;

	public sealed override int FieldCount => _FieldCount;

	public sealed override LatentFieldVal ReadFieldValLater(int index) {
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

			return new(stream, _FieldValListPos + fOffset, fValLen);
		} else {
			return OnReadFieldValLaterOutOfRange(index);
		}
	}

	public sealed override FieldVal ReadFieldVal(int index) {
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

				FieldTypeHint typeHint = (FieldTypeHint)fValSpec;
				var data = new byte[fValLen - fValSpecLen];
				{
					int sread = stream.Read(data);
					Debug.Assert(sread == data.Length);
				}

				return new(typeHint, data);
			} else {
				return FieldVal.Null;
			}
		} else {
			return OnReadFieldValOutOfRange(index);
		}
	}

	protected virtual LatentFieldVal OnReadFieldValLaterOutOfRange(int index)
		=> new(_Stream, _Stream.Length, 0);

	protected virtual FieldVal OnReadFieldValOutOfRange(int index) => FieldVal.Null;

	public override void Dispose() => _Stream.Dispose();
}
