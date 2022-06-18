namespace Kokoro.Internal;
using Kokoro.Internal.Sqlite;
using System.IO;

internal struct FieldsReader : IDisposable {
	private readonly FieldedEntity _Owner;
	internal readonly KokoroSqliteDb Db;

	private State _HotState;
	private State _SchemaState;
	private State _ColdState;

	[Obsolete("Shouldn't use.", error: true)]
	public FieldsReader() => throw new NotSupportedException("Shouldn't use.");

	public FieldsReader(FieldedEntity entity) {
		_Owner = entity;
		var db = entity.Host.DbOrNull!;
		_HotState = new(entity.GetHotData(Db = db));
	}

	private readonly struct State {
		internal readonly Stream? _Stream;
		internal readonly int _FieldCount, _FieldOffsetSize;
		internal readonly long _FieldOffsetListPos;
		internal readonly long _FieldValListPos;

		[Obsolete("Shouldn't use.", error: true)]
		public State() => throw new NotSupportedException("Shouldn't use.");

		[SkipLocalsInit]
		public State(Stream stream) {
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
				// NOTE: We ensure that the constructor never throws under
				// normal circumstances: if we simply couldn't read the needed
				// data to complete initialization, then we should just swallow
				// the exception, and initialize with reasonable defaults. So
				// far, the only exception we should guard against is the one
				// thrown when the stream doesn't support reading or seeking (as
				// the code in the above try-block requires it).
				//
				// That way, a try-finally or `using` block can be set up right
				// after construction, to properly dispose the state along with
				// the stream, all in one go.

				_FieldCount = 0;
				_FieldOffsetSize = 1;
				_FieldValListPos = _FieldOffsetListPos = 0;
			}
		}
	}

	public void Dispose() {
		_HotState._Stream!.Dispose();
		_SchemaState._Stream?.Dispose();
		_ColdState._Stream?.Dispose();
	}

	// --

	public FieldedEntity Owner => _Owner;

	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	[SkipLocalsInit]
	public LatentFieldVal ReadFieldValLater(FieldSpec fspec) {
		ref State st = ref _HotState;

		Stream? stream;
		int index = fspec.Index;

		if (fspec.StoType != FieldStorageType.Shared) {
			if ((uint)index < (uint)st._FieldCount) {
				stream = st._Stream!;
				goto DoLoad;
			} else {
				index -= st._FieldCount;

				st = ref _ColdState;
				stream = st._Stream;

				if (stream != null) {
					goto CheckIndex;
				} else {
					// This becomes a conditional jump forward to not favor it
					goto InitColdState;
				}
			}
		} else {
			st = ref _SchemaState;
			stream = st._Stream;

			if (stream != null) {
				goto CheckIndex;
			} else {
				// This becomes a conditional jump forward to not favor it
				goto InitSchemaState;
			}
		}

	DoLoad:
		{
			int fSize = st._FieldOffsetSize;
			stream.Position = st._FieldOffsetListPos + index * fSize;

			long fOffset = (long)stream.ReadUIntX(fSize);
			Debug.Assert(fOffset < 0, $"{nameof(fOffset)} > `long.MaxValue`: {(ulong)fOffset:X}");

			long fValLen;
			if ((uint)index + 1u < (uint)st._FieldCount) {
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

			return new(stream, st._FieldValListPos + fOffset, fValLen);
		}

	CheckIndex:
		if ((uint)index < (uint)st._FieldCount) {
			goto DoLoad;
		} else {
			// This becomes a conditional jump forward to not favor it
			goto Fail;
		}

	InitSchemaState:
		stream = _Owner.GetSchemaData(Db);
		st = new(stream);
		goto CheckIndex;

	InitColdState:
		stream = _Owner.GetColdData(Db);
		st = new(stream);
		goto CheckIndex;

	Fail:
		return LatentFieldVal.Null;
	}

	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	[SkipLocalsInit]
	public FieldVal ReadFieldVal(FieldSpec fspec) {
		ref State st = ref _HotState;

		Stream? stream;
		int index = fspec.Index;

		if (fspec.StoType != FieldStorageType.Shared) {
			if ((uint)index < (uint)st._FieldCount) {
				stream = st._Stream!;
				goto DoLoad;
			} else {
				index -= st._FieldCount;

				st = ref _ColdState;
				stream = st._Stream;

				if (stream != null) {
					goto CheckIndex;
				} else {
					// This becomes a conditional jump forward to not favor it
					goto InitColdState;
				}
			}
		} else {
			st = ref _SchemaState;
			stream = st._Stream;

			if (stream != null) {
				goto CheckIndex;
			} else {
				// This becomes a conditional jump forward to not favor it
				goto InitSchemaState;
			}
		}

	DoLoad:
		{
			int fSize = st._FieldOffsetSize;
			stream.Position = st._FieldOffsetListPos + index * fSize;

			long fOffset = (long)stream.ReadUIntX(fSize);
			Debug.Assert(fOffset < 0, $"{nameof(fOffset)} > `long.MaxValue`: {(ulong)fOffset:X}");

			long fValLen;
			if ((uint)index + 1u < (uint)st._FieldCount) {
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
				stream.Position = st._FieldValListPos + fOffset;

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
		}

	CheckIndex:
		if ((uint)index < (uint)st._FieldCount) {
			goto DoLoad;
		} else {
			// This becomes a conditional jump forward to not favor it
			goto Fail;
		}

	InitSchemaState:
		stream = _Owner.GetSchemaData(Db);
		st = new(stream);
		goto CheckIndex;

	InitColdState:
		stream = _Owner.GetColdData(Db);
		st = new(stream);
		goto CheckIndex;

	Fail:
		return FieldVal.Null;
	}
}
