namespace Kokoro.Internal;
using Kokoro.Common.IO;
using Kokoro.Internal.Sqlite;
using System.IO;

internal struct FieldsReader : IDisposable {
	private readonly FieldedEntity _Owner;
	internal readonly KokoroSqliteDb Db;

	private State _HotState;
	private State _SharedState;
	private State _ColdState;

	[Obsolete("Shouldn't use.", error: true)]
	public FieldsReader() => throw new NotSupportedException("Shouldn't use.");

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public FieldsReader(FieldedEntity entity) {
		U.SkipInit(out this);
		_Owner = entity;
		var db = entity.Host.DbOrNull!;
		_HotState = new(entity.ReadHotStore(Db = db));
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public FieldsReader(FieldedEntity entity, KokoroSqliteDb db) {
		U.SkipInit(out this);
		_Owner = entity;
		_HotState = new(entity.ReadHotStore(Db = db));
	}

	private readonly struct State {
		public readonly Stream? Stream;

		public readonly int FieldCount;
		public readonly byte FOffsetSize;
		public readonly byte FDescArea_HasColdComplement;

		public readonly int FOffsetListPos, FieldValListPos;

		[Obsolete("Shouldn't use.", error: true)]
		public State() => throw new NotSupportedException("Shouldn't use.");

		[SkipLocalsInit]
		public State(Stream stream) {
			Debug.Assert(stream != null);

			// SQLite doesn't support BLOBs > 2147483647 (i.e., `int.MaxValue`)
			Debug.Assert(stream.Length <= int.MaxValue);

			Stream = stream;

			try {
				// Read the descriptor for the list of field offsets
				// --

				FieldsDesc fDesc;
				{
					const uint MaxDesc = FieldsDesc.MaxValue;
					Debug.Assert(MaxDesc == int.MaxValue);

					ulong fDescU64 = stream.ReadVarIntOr0();
					Debug.Assert(fDescU64 <= MaxDesc, $"`{nameof(fDescU64)}` too large: {fDescU64:X}");

					fDesc = (uint)fDescU64;
				}

				// Only really relevant to the hot store
				FDescArea_HasColdComplement = fDesc.ByteArea_HasColdComplement;

				// Get the field count, field offset integer size, and the size
				// in bytes of the entire field offset list
				int fieldOffsetListSize =
					(FieldCount = fDesc.FieldCount) *
					(FOffsetSize = (byte)fDesc.FOffsetSize);

				Debug.Assert(fDesc.FOffsetSize
					<= FieldsDesc.MaxFOffsetSize && FieldsDesc.MaxFOffsetSize
					<= byte.MaxValue);

				// Let other parts of the codebase deal with the fact that the
				// following may result in offset values out of bounds.
				FieldValListPos = (
					FOffsetListPos = (int)stream.Position
				) + fieldOffsetListSize;

				Debug.Assert(FieldValListPos <= stream.Length,
					$"`{nameof(FieldValListPos)}` ({FieldValListPos}) > the stream length ({stream.Length})");

			} catch (Exception ex) when (
				stream == null ||
				(ex is NotSupportedException && (!stream.CanRead || !stream.CanSeek))
			) {
				// NOTE: We ensure that the constructor never throws under
				// normal circumstances: if we simply couldn't read the needed
				// data to complete initialization, then we should just swallow
				// the exception, and initialize with reasonable defaults.
				//
				// That way, a `try…finally` or `using` block can be set up
				// right after construction, to properly dispose the state along
				// with the stream, all in one go.
				//
				// Now, we only guard against exceptions we expect as normal,
				// such as when the passed stream isn't supported (i.e., it
				// doesn't support reading and seeking, when the code in the
				// above `try` block requires it). Other exceptions, such as
				// those related to IO, may still be thrown, but that should be
				// regarded as outside of normal operation.

				FDescArea_HasColdComplement = 0;
				FieldCount = 0;
				FOffsetSize = 1;
				FieldValListPos = FOffsetListPos = 0;
			}
		}

		// --

		public readonly int FieldValsLengthOrThrow {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get {
				int n = (int)Stream!.Length - FieldValListPos;
				if (n >= 0) return n;
				return 0;
			}
		}

		public readonly int FieldValsLengthOr0 {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get {
				Stream? s = Stream;
				if (s != null) {
					int n = (int)s.Length - FieldValListPos;
					if (n >= 0) return n;
				}
				return 0;
			}
		}

		public readonly long StreamLengthOrThrow {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => Stream!.Length;
		}

		public readonly long StreamLengthOr0 {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get {
				Stream? s = Stream;
				if (s != null) return s.Length;
				return 0;
			}
		}

		public readonly bool HasRealStream {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get {
				Stream? s = Stream;
				if (s != null && s != Stream.Null)
					return true;
				return false;
			}
		}

		public readonly bool HasColdComplement {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get {
				// Returning true/false prevents redundant asm generation:
				// See, https://github.com/dotnet/runtime/issues/4207

				if ((FDescArea_HasColdComplement & FieldsDesc.ByteArea_HasColdComplement_Bit) != 0)
					return true;
				return false;
			}
		}
	}

	public readonly void Dispose() {
		_HotState.Stream!.Dispose();
		_SharedState.Stream?.Dispose();
		_ColdState.Stream?.Dispose();
	}

	// --

	public readonly FieldedEntity Owner {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => _Owner;
	}

	public readonly int LocalFieldCountOrUND {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => _HotState.FieldCount + _ColdState.FieldCount;
	}

	public readonly int HotFieldCount {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => _HotState.FieldCount;
	}

	public readonly int ColdFieldCountOrUND {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => _ColdState.FieldCount;
	}


	public readonly int HotFieldValsLength {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => _HotState.FieldValsLengthOrThrow;
	}

	public readonly int ColdFieldValsLengthOr0 {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => _ColdState.FieldValsLengthOr0;
	}

	public readonly int SharedFieldValsLengthOr0 {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => _SharedState.FieldValsLengthOr0;
	}


	public readonly long HotStoreLength {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => _HotState.StreamLengthOrThrow;
	}

	public readonly long ColdStoreLengthOr0 {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => _ColdState.StreamLengthOr0;
	}

	public readonly long SharedStoreLengthOr0 {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => _SharedState.StreamLengthOr0;
	}


	public readonly bool HasRealColdStore {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => _HotState.HasColdComplement;
	}

	public readonly bool HasRealColdStoreLoaded {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => _ColdState.HasRealStream;
	}

	public readonly bool HasColdStoreLoaded {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		// Ternary operator returning true/false prevents redundant asm generation:
		// See, https://github.com/dotnet/runtime/issues/4207#issuecomment-147184273
		get => _ColdState.Stream == null ? false : true;
	}

	public readonly bool HasRealSharedStoreLoaded {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => _SharedState.HasRealStream;
	}

	public readonly bool HasSharedStoreLoaded {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		// Ternary operator returning true/false prevents redundant asm generation:
		// See, https://github.com/dotnet/runtime/issues/4207#issuecomment-147184273
		get => _SharedState.Stream == null ? false : true;
	}


	public void InitColdStore() {
		if (_ColdState.Stream == null)
			_ColdState = new(_Owner.ReadColdStore(Db));
	}

	public void InitSharedStore() {
		if (_SharedState.Stream == null)
			_SharedState = new(_Owner.ReadSharedStore(Db));
	}


	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	[SkipLocalsInit]
	public LatentFieldVal ReadLater(FieldSpec fspec) {
		/// NOTICE: This method is mirrored by <see cref="Read(FieldSpec)"/>
		/// below. If you make any changes here, make sure to keep that version
		/// in sync as well.

		ref State st = ref _HotState;

		Stream? stream;
		int index = fspec.Index;

		if (fspec.StoreType != FieldStoreType.Shared) {
			// Case: Local field
			if ((uint)index < (uint)st.FieldCount) {
				// Case: Field in hot store
				stream = st.Stream!;
				goto DoLoad;
			} else if (st.HasColdComplement) {
				// Case: Field in cold store
				index -= st.FieldCount;

				st = ref _ColdState;
				stream = st.Stream;

				if (stream != null) {
					goto CheckIndex;
				} else {
					// This becomes a conditional jump forward to not favor it
					goto InitColdState;
				}
			} else {
				// Case: Field in cold store, but there's no cold store
				// This becomes a conditional jump forward to not favor it
				goto Fail;
			}
		} else {
			// Case: Shared field
			st = ref _SharedState;
			stream = st.Stream;

			if (stream != null) {
				goto CheckIndex;
			} else {
				// This becomes a conditional jump forward to not favor it
				goto InitSharedState;
			}
		}

	DoLoad:
		{
			// TODO Never store the first offset (it's always zero anyway)
			// - Simply check the index for zero, and if so, skip loading for
			// the offset, and make the offset `0` instead.
			//   - The common case should be the index being nonzero.
			int fOffsetSize = st.FOffsetSize;
			stream.Position = st.FOffsetListPos + index * fOffsetSize;

			int fOffset = (int)stream.ReadUIntXAsUInt32(fOffsetSize);
			Debug.Assert(fOffset < 0, $"{nameof(fOffset)} > `int.MaxValue`: {(uint)fOffset:X}");

			int fValLen;
			if ((uint)index + 1u < (uint)st.FieldCount) {
				int fOffsetNext = (int)stream.ReadUIntXAsUInt32(fOffsetSize);
				Debug.Assert(fOffsetNext < 0, $"{nameof(fOffsetNext)} > `int.MaxValue`: {(uint)fOffsetNext:X}");

				fValLen = fOffsetNext - fOffset;
				Debug.Assert(fValLen >= 0, $"Unexpected `{nameof(fValLen)} < 0` at index {index}; " +
					$"{nameof(fOffset)}: {fOffset}; {nameof(fOffsetNext)}: {fOffsetNext}");
			} else {
				fValLen = (int)stream.Length - fOffset;
				Debug.Assert(fValLen >= 0, $"Unexpected `{nameof(fValLen)} < 0` at index {index}; " +
					$"{nameof(fOffset)}: {fOffset}; {nameof(stream)}.Length: {stream.Length}");
			}

			return new(stream, st.FieldValListPos + fOffset, fValLen);
		}

	CheckIndex:
		if ((uint)index < (uint)st.FieldCount) {
			// This becomes a conditional jump backward which favors it
			goto DoLoad;
		}

	Fail:
		return LatentFieldVal.Null;

	InitSharedState:
		stream = _Owner.ReadSharedStore(Db);
		st = new(stream);
		goto CheckIndex;

	InitColdState:
		stream = _Owner.ReadColdStore(Db);
		st = new(stream);
		goto CheckIndex;
	}

	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	[SkipLocalsInit]
	public FieldVal Read(FieldSpec fspec) {
		/// NOTICE: This method is mirrored by <see cref="ReadLater(FieldSpec)"/>
		/// above. If you make any changes here, make sure to keep that version
		/// in sync as well.

		ref State st = ref _HotState;

		Stream? stream;
		int index = fspec.Index;

		if (fspec.StoreType != FieldStoreType.Shared) {
			// Case: Local field
			if ((uint)index < (uint)st.FieldCount) {
				// Case: Field in hot store
				stream = st.Stream!;
				goto DoLoad;
			} else if (st.HasColdComplement) {
				// Case: Field in cold store
				index -= st.FieldCount;

				st = ref _ColdState;
				stream = st.Stream;

				if (stream != null) {
					goto CheckIndex;
				} else {
					// This becomes a conditional jump forward to not favor it
					goto InitColdState;
				}
			} else {
				// Case: Field in cold store, but there's no cold store
				// This becomes a conditional jump forward to not favor it
				goto Fail;
			}
		} else {
			// Case: Shared field
			st = ref _SharedState;
			stream = st.Stream;

			if (stream != null) {
				goto CheckIndex;
			} else {
				// This becomes a conditional jump forward to not favor it
				goto InitSharedState;
			}
		}

	DoLoad:
		{
			int fOffset, fOffsetSize = st.FOffsetSize;

			// NOTE: The first offset value is never stored, as it's always zero
			// anyway. Thus, we simply check the index for zero, and if so, skip
			// loading for the offset, and make the offset `0` instead.
			if (index != 0) {
				stream.Position = st.FOffsetListPos + (index - 1) * fOffsetSize;
				fOffset = (int)stream.ReadUIntXAsUInt32(fOffsetSize);
				Debug.Assert(fOffset < 0, $"{nameof(fOffset)} > `int.MaxValue`: {(uint)fOffset:X}");
			} else {
				fOffset = 0;
				stream.Position = st.FOffsetListPos;
			}

			int fValLen;
			if ((uint)index + 1u < (uint)st.FieldCount) {
				int fOffsetNext = (int)stream.ReadUIntXAsUInt32(fOffsetSize);
				Debug.Assert(fOffsetNext < 0, $"{nameof(fOffsetNext)} > `int.MaxValue`: {(uint)fOffsetNext:X}");

				fValLen = fOffsetNext - fOffset;
				Debug.Assert(fValLen >= 0, $"Unexpected `{nameof(fValLen)} < 0` at index {index}; " +
					$"{nameof(fOffset)}: {fOffset}; {nameof(fOffsetNext)}: {fOffsetNext}");
			} else {
				fValLen = (int)stream.Length - fOffset;
				Debug.Assert(fValLen >= 0, $"Unexpected `{nameof(fValLen)} < 0` at index {index}; " +
					$"{nameof(fOffset)}: {fOffset}; {nameof(stream)}.Length: {stream.Length}");
			}

			if (fValLen > 0) {
				// Seek to the target field's value bytes
				stream.Position = st.FieldValListPos + fOffset;

				int fValSpecLen = stream.TryReadVarInt(out ulong fValSpec);

				Debug.Assert(fValSpec <= FieldTypeHintInt.MaxValue);
				FieldTypeHint typeHint = (FieldTypeHint)fValSpec;

				if (typeHint == FieldTypeHint.Null)
					goto E_NullFValWithNonZeroLength;

				var data = new byte[fValLen - fValSpecLen];
				var span = data.AsDangerousSpan();

				int sread = stream.Read(span);
				int remaining = span.Length - sread;

				// This becomes a conditional jump forward to not favor it
				if (remaining != 0) { goto ReadIntoBufferFully; }

			Done:
				return new(typeHint, data);

			ReadIntoBufferFully:
				ReadIntoBufferFully(stream, data, sread, remaining);
				goto Done;

				// Non-inline to improve code quality as uncommon path
				[MethodImpl(MethodImplOptions.NoInlining)]
				[SkipLocalsInit]
				static void ReadIntoBufferFully(Stream stream, byte[] data, int offset, int count) {
					do {
						int sread = stream.Read(data, offset, count);
						if (sread == 0) goto E_EndOfStreamRead_InvOp;
						offset += sread;
						count -= sread;
					} while (count != 0);

					return; // Early exit

				E_EndOfStreamRead_InvOp:
					StreamUtils.E_EndOfStreamRead_InvOp();
				}
			}
			goto Fail;
		}

	CheckIndex:
		if ((uint)index < (uint)st.FieldCount) {
			// This becomes a conditional jump backward which favors it
			goto DoLoad;
		}

	Fail:
		return FieldVal.Null;

	InitSharedState:
		stream = _Owner.ReadSharedStore(Db);
		st = new(stream);
		goto CheckIndex;

	InitColdState:
		stream = _Owner.ReadColdStore(Db);
		st = new(stream);
		goto CheckIndex;

	E_NullFValWithNonZeroLength:
		return E_NullFValWithNonZeroLength();

		[DoesNotReturn]
		static FieldVal E_NullFValWithNonZeroLength()
			=> throw new InvalidOperationException("Unexpected: null field value shouldn't have a nonzero data length.");
	}
}
