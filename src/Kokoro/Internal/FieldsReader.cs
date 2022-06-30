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

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public FieldsReader(FieldedEntity entity) {
		_Owner = entity;
		var db = entity.Host.DbOrNull!;
		_HotState = new(entity.ReadHotStore(Db = db));
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public FieldsReader(FieldedEntity entity, KokoroSqliteDb db) {
		_Owner = entity;
		_HotState = new(entity.ReadHotStore(Db = db));
	}

	private readonly struct State {
		public readonly Stream? Stream;
		public readonly int FieldCount, FOffsetSize;
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
				// --
				// Read the descriptor for the list of field offsets
				FieldsDesc fDesc;
				{
					const uint MaxDesc = FieldsDesc.MaxValue;
					Debug.Assert(MaxDesc == int.MaxValue);

					ulong fDescU64 = stream.ReadVarIntOr0();
					Debug.Assert(fDescU64 <= MaxDesc, $"`{nameof(fDescU64)}` too large: {fDescU64:X}");

					fDesc = (uint)fDescU64;
				}

				// Get the field count, field offset integer size, and the size
				// in bytes of the entire field offset list
				int fieldOffsetListSize =
					(FieldCount = fDesc.FieldCount) *
					(FOffsetSize = fDesc.FOffsetSize);

				// --

				uint u_fieldValListPos = (uint)checked(
					FieldValListPos = (
						FOffsetListPos = unchecked((int)stream.Position)
					) + fieldOffsetListSize
				);

				// NOTE: If the following won't throw, the `(int)stream.Position`
				// before us was truncated as a positve integer successfully.
				uint u_streamLength = (uint)checked((int)unchecked((ulong)stream.Length));

				/// Ensure <see cref="HotFieldValsLength"/> will never return a
				/// negative value.
				_ = checked(u_streamLength - u_fieldValListPos);

			} catch (Exception ex) when (
				ex is OverflowException || stream == null ||
				(ex is NotSupportedException && (!stream.CanRead || !stream.CanSeek))
			) {
				// NOTE: We ensure that the constructor never throws under
				// normal circumstances: if we simply couldn't read the needed
				// data to complete initialization, then we should just swallow
				// the exception, and initialize with reasonable defaults. Other
				// than the overflow check, the only other exception we must
				// guard against for now is the one thrown when the stream does
				// not support reading or seeking (as the code in the above
				// try-block requires it).
				//
				// That way, a try-finally or `using` block can be set up right
				// after construction, to properly dispose the state along with
				// the stream, all in one go.

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
				DAssert_NonNegByConstruction(n);
				return n;
			}
		}

		public readonly int FieldValsLengthOr0 {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get {
				Stream? s = Stream;
				if (s != null) {
					int n = (int)s.Length - FieldValListPos;
					DAssert_NonNegByConstruction(n);
					return n;
				}
				return 0;
			}
		}

		public readonly int StreamLengthOrThrow {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get {
				long n = Stream!.Length;
				DAssert_PosInt32(n);
				return (int)n;
			}
		}

		public readonly int StreamLengthOr0 {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get {
				Stream? s = Stream;
				if (s != null) {
					long n = s.Length;
					DAssert_PosInt32(n);
					return (int)n;
				}
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

		// --

		[Conditional("DEBUG")]
		private static void DAssert_NonNegByConstruction(int n)
			=> Debug.Assert(n >= 0, "Constructor should've ensured this to be non-negative.");

		[Conditional("DEBUG")]
		private static void DAssert_PosInt32(long n)
			=> Debug.Assert((ulong)n < (ulong)int.MaxValue,
				"Constructor should've ensured that `0 <= n <= int.MaxValue` for `n`.");
	}

	public void Dispose() {
		_HotState.Stream!.Dispose();
		_SchemaState.Stream?.Dispose();
		_ColdState.Stream?.Dispose();
	}

	// --

	public FieldedEntity Owner => _Owner;

	public int HotFieldCount {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => _HotState.FieldCount;
	}


	public int HotFieldValsLength {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => _HotState.FieldValsLengthOrThrow;
	}

	public int SharedFieldValsLengthOr0 {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => _SchemaState.FieldValsLengthOr0;
	}

	public int ColdFieldValsLengthOr0 {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => _ColdState.FieldValsLengthOr0;
	}


	public int HotStoreLength {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => _HotState.StreamLengthOrThrow;
	}

	public int SharedStoreLengthOr0 {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => _SchemaState.StreamLengthOr0;
	}

	public int ColdStoreLengthOr0 {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => _ColdState.StreamLengthOr0;
	}


	public bool HasSharedStoreLoaded {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		// Ternary operator returning true/false prevents redundant asm generation:
		// See, https://github.com/dotnet/runtime/issues/4207#issuecomment-147184273
		get => _SchemaState.Stream == null ? false : true;
	}

	public bool HasRealSharedStoreLoaded {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => _SchemaState.HasRealStream;
	}

	public bool HasColdStoreLoaded {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		// Ternary operator returning true/false prevents redundant asm generation:
		// See, https://github.com/dotnet/runtime/issues/4207#issuecomment-147184273
		get => _ColdState.Stream == null ? false : true;
	}

	public bool HasRealColdStoreLoaded {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => _ColdState.HasRealStream;
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
		var sto = fspec.StoreType;

		if (sto != FieldStoreType.Shared) {
			// Case: local field
			if ((uint)index < (uint)st.FieldCount) {
				// Case: hot field
				stream = st.Stream!;
				goto DoLoad;
			} else if (sto != FieldStoreType.Hot) {
				// Case: cold field
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
				// Case: hot field (but out of bounds)
				// This becomes a conditional jump forward to not favor it
				goto Fail;
			}
		} else {
			// Case: shared field (located at the schema-level)
			st = ref _SchemaState;
			stream = st.Stream;

			if (stream != null) {
				goto CheckIndex;
			} else {
				// This becomes a conditional jump forward to not favor it
				goto InitSchemaState;
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
				fValLen = (int)(stream.Length - fOffset);
				Debug.Assert(fValLen >= 0, $"Unexpected `{nameof(fValLen)} < 0` at index {index}; " +
					$"{nameof(fOffset)}: {fOffset}; {nameof(stream)}.Length: {stream.Length}");
			}

			return new(stream, st.FieldValListPos + fOffset, fValLen);
		}

	Fail:
		return LatentFieldVal.Null;

	CheckIndex:
		if ((uint)index < (uint)st.FieldCount) {
			goto DoLoad;
		} else {
			// This becomes a conditional jump forward to not favor it
			goto Fail;
		}

	InitSchemaState:
		stream = _Owner.ReadSchemaStore(Db);
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
		var sto = fspec.StoreType;

		if (sto != FieldStoreType.Shared) {
			// Case: local field
			if ((uint)index < (uint)st.FieldCount) {
				// Case: hot field
				stream = st.Stream!;
				goto DoLoad;
			} else if (sto != FieldStoreType.Hot) {
				// Case: cold field
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
				// Case: hot field (but out of bounds)
				// This becomes a conditional jump forward to not favor it
				goto Fail;
			}
		} else {
			// Case: shared field (located at the schema-level)
			st = ref _SchemaState;
			stream = st.Stream;

			if (stream != null) {
				goto CheckIndex;
			} else {
				// This becomes a conditional jump forward to not favor it
				goto InitSchemaState;
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
				fValLen = (int)(stream.Length - fOffset);
				Debug.Assert(fValLen >= 0, $"Unexpected `{nameof(fValLen)} < 0` at index {index}; " +
					$"{nameof(fOffset)}: {fOffset}; {nameof(stream)}.Length: {stream.Length}");
			}

			if (fValLen > 0) {
				// Seek to the target field's value bytes
				stream.Position = st.FieldValListPos + fOffset;

				int fValSpecLen = stream.TryReadVarInt(out ulong fValSpec);
				Debug.Assert(fValSpec <= FieldTypeHintInt.MaxValue);

				FieldTypeHint typeHint = (FieldTypeHint)fValSpec;
				if (typeHint != FieldTypeHint.Null) {
					var data = new byte[fValLen - fValSpecLen];
					{
						int sread = stream.Read(data);
						Debug.Assert(sread == data.Length);
					}
					return new(typeHint, data);
				}
			}
			// Fallthrough
		}

	Fail:
		return FieldVal.Null;

	CheckIndex:
		if ((uint)index < (uint)st.FieldCount) {
			goto DoLoad;
		} else {
			// This becomes a conditional jump forward to not favor it
			goto Fail;
		}

	InitSchemaState:
		stream = _Owner.ReadSchemaStore(Db);
		st = new(stream);
		goto CheckIndex;

	InitColdState:
		stream = _Owner.ReadColdStore(Db);
		st = new(stream);
		goto CheckIndex;
	}
}
