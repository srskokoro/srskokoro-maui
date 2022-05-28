﻿namespace Kokoro.Internal.Marshal.Fields;
using System.IO;

internal abstract partial class BaseFieldsReader<TOwner> {

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor.
	private BaseFieldsReader() { }
#pragma warning restore CS8618

	public abstract partial class WithModStamps : BaseFieldsReader<TOwner> {

		private int _ModStampCount, _ModStampSize;
		private long _ModStampListPos;

		public WithModStamps(TOwner owner, Stream stream) {
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
				// Read the descriptor for the list of modstamps
				ulong mDesc = stream.ReadVarIntOrZero();
				Debug.Assert(mDesc <= MaxDesc, $"`{nameof(mDesc)}` too large: {mDesc:X}");

				// Get the modstamp count and modstamp integer size
				int mCount = (int)(mDesc >> 3);
				int mSize = ((int)mDesc & 0b111) + 1;

				_ModStampCount = mCount;
				_ModStampSize = mSize;

				// The size in bytes of the entire modstamp list
				int modstampListSize = mCount * mSize;

				// --

				_FieldValListPos = (_ModStampListPos = (_FieldOffsetListPos = stream.Position) + fieldOffsetListSize) + modstampListSize;

			} catch (NotSupportedException) when (!stream.CanRead || !stream.CanSeek) {
				// NOTE: We ensure that the constructor never throws under
				// normal circumstances: if we simply couldn't read the needed
				// data to complete initialization, then we should just swallow
				// the exception, and initialize with reasonable defaults. So
				// far, the only exception we should guard against is the one
				// thrown when the stream doesn't support reading or seeking (as
				// the code in the above try-block requires it).

				_FieldCount = _ModStampCount = 0;
				_FieldOffsetSize = _ModStampSize = 1;
				_FieldValListPos = _ModStampListPos = _FieldOffsetListPos = 0;
			}
		}

		// --

		public sealed override int ModStampCount => _ModStampCount;

		public sealed override long ReadModStamp(int index) {
			if ((uint)index < (uint)_ModStampCount) {
				var stream = _Stream;

				int mSize = _ModStampSize;
				stream.Position = _ModStampListPos + index * mSize;

				return (long)stream.ReadUIntX(mSize);
			} else {
				return OnReadModStampOutOfRange(index);
			}
		}

		protected virtual long OnReadModStampOutOfRange(int index)
			=> throw new ArgumentOutOfRangeException(nameof(index));
	}
}
