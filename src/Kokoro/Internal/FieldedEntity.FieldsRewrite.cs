namespace Kokoro.Internal;
using Kokoro.Common.Buffers;
using Kokoro.Common.Util;
using Microsoft.Data.Sqlite;
using System.Buffers;
using System.Runtime.InteropServices;

partial class FieldedEntity {
	private const int MaxFieldCount = byte.MaxValue + 1;
	private const int MaxFieldValsLength = 0xFF_FFFF;

	private struct FieldsWriterCore {
		internal int[] Offsets;
		internal FieldsWriter.Entry[] Entries;
		internal (FieldSpec FSpec, FieldVal FVal)[] FOverrides;

		[MethodImpl(MethodImplOptions.AggressiveOptimization)]
		[SkipLocalsInit]
		internal readonly int Load(ref FieldsReader fr, int nextOffset, int start, int end) {
			try {
				Debug.Assert((uint)end <= (uint)MaxFieldCount);

				Debug.Assert((uint)end <= (uint?)Offsets?.Length); // `false` on null array
				Debug.Assert((uint)end <= (uint?)Entries?.Length);
				Debug.Assert(FOverrides != null);

				// Get references to avoid unnecessary range checking
				ref var foverride = ref FOverrides.DangerousGetReference();
				ref var entries_r0 = ref Entries.DangerousGetReference();
				ref var offsets_r0 = ref Offsets.DangerousGetReference();

				Types.DAssert_IsReferenceOrContainsReferences(entries_r0);

				for (int i = start; i < end; i++) {
					U.Add(ref offsets_r0, i) = nextOffset;
					ref var entry = ref U.Add(ref entries_r0, i);

					if (foverride.FSpec.Index != i) {
						LatentFieldVal lfval = fr.ReadLater(new(i, FieldStoreType.Cold));
						entry.OrigValue = lfval;

						// It's a reference type: it should've been
						// automatically initialized to null.
						Debug.Assert(entry.Override == null);

						int nextLength = lfval.Length;
						if (nextLength >= 0) {
							checked { nextOffset += nextLength; }
						} else {
							entry.Override = FieldVal.Null;
						}
					} else {
						FieldVal fval = foverride.FVal;
						entry.Override = fval;

						do foverride = ref U.Add(ref foverride, 1);
						while (foverride.FSpec.Index != i);

						checked {
							nextOffset += (int)fval.CountEncodeLength();
						}
					}
				}
			} catch (OverflowException) {
				goto E_FieldValsLengthTooLarge;
			}

			if (nextOffset <= MaxFieldValsLength) {
				return nextOffset; // Early exit
			}

		E_FieldValsLengthTooLarge:
			return E_FieldValsLengthTooLarge<int>((uint)nextOffset);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal readonly int LoadHot(ref FieldsReader fr, int end)
			=> Load(ref fr, nextOffset: 0, start: 0, end);

		[MethodImpl(MethodImplOptions.AggressiveOptimization)]
		[SkipLocalsInit]
		internal readonly int LoadHotNoOverride(ref FieldsReader fr, int end) {
			int nextOffset = 0;
			try {
				Debug.Assert((uint)end <= (uint)MaxFieldCount);

				Debug.Assert((uint)end <= (uint?)Offsets?.Length); // `false` on null array
				Debug.Assert((uint)end <= (uint?)Entries?.Length);

				// Get references to avoid unnecessary range checking
				ref var entries_r0 = ref Entries.DangerousGetReference();
				ref var offsets_r0 = ref Offsets.DangerousGetReference();

				Types.DAssert_IsReferenceOrContainsReferences(entries_r0);

				for (int i = 0; i < end; i++) {
					U.Add(ref offsets_r0, i) = nextOffset;
					ref var entry = ref U.Add(ref entries_r0, i);

					LatentFieldVal lfval = fr.ReadLater(new(i, FieldStoreType.Cold));
					entry.OrigValue = lfval;

					// It's a reference type: it should've been
					// automatically initialized to null.
					Debug.Assert(entry.Override == null);

					int nextLength = lfval.Length;
					if (nextLength >= 0) {
						checked { nextOffset += nextLength; }
					} else {
						entry.Override = FieldVal.Null;
					}
				}
			} catch (OverflowException) {
				goto E_FieldValsLengthTooLarge;
			}

			// NOTE: Given that everything was loaded from the old field stores,
			// we won't check whether or not the loaded data is within the
			// maximum allowed length.
			return nextOffset; // Early exit

		E_FieldValsLengthTooLarge:
			return E_FieldValsLengthTooLarge<int>((uint)nextOffset);
		}

		[MethodImpl(MethodImplOptions.AggressiveOptimization)]
		[SkipLocalsInit]
		internal readonly int LoadColdNoRead(int nextOffset, int start, int end) {
			try {
				Debug.Assert((uint)end <= (uint)MaxFieldCount);

				Debug.Assert((uint)end <= (uint?)Offsets?.Length); // `false` on null array
				Debug.Assert((uint)end <= (uint?)Entries?.Length);
				Debug.Assert(FOverrides != null);

				// Get references to avoid unnecessary range checking
				ref var foverride = ref FOverrides.DangerousGetReference();
				ref var entries_r0 = ref Entries.DangerousGetReference();
				ref var offsets_r0 = ref Offsets.DangerousGetReference();

				Types.DAssert_IsReferenceOrContainsReferences(entries_r0);

				for (int i = start; i < end; i++) {
					U.Add(ref offsets_r0, i) = nextOffset;
					ref var entry = ref U.Add(ref entries_r0, i);

					if (foverride.FSpec.Index != i) {
						entry.OrigValue = LatentFieldVal.Null;

						// Assert that we don't have to adjust the next offset
						Debug.Assert(entry.OrigValue.Length == 0);

						// It's a reference type: it should've been
						// automatically initialized to null.
						Debug.Assert(entry.Override == null);

					} else {
						FieldVal fval = foverride.FVal;
						entry.Override = fval;

						do foverride = ref U.Add(ref foverride, 1);
						while (foverride.FSpec.Index != i);

						checked {
							nextOffset += (int)fval.CountEncodeLength();
						}
					}
				}
			} catch (OverflowException) {
				goto E_FieldValsLengthTooLarge;
			}

			if (nextOffset <= MaxFieldValsLength) {
				return nextOffset; // Early exit
			}

		E_FieldValsLengthTooLarge:
			return E_FieldValsLengthTooLarge<int>((uint)nextOffset);
		}

		[MethodImpl(MethodImplOptions.AggressiveOptimization)]
		[SkipLocalsInit]
		internal readonly int TrimNullFValsFromEnd(int end) {
			// Ensure caller passed valid arguments
			Debug.Assert((uint)end <= (uint?)Entries?.Length);

			// Get a reference to avoid unnecessary range checking
			ref var entries_r0 = ref Entries.DangerousGetReference();

			// NOTE: `end` is excluded, as you'll see later below
			int i = end;

			for (; ; ) {
				if (--i < 0) break;
				// ^- Better IL & asm than `while` loop equivalent. The `while`
				// loop would get inverted, with the condition at the loop's
				// footer. But, we want the `continue` statements (see below) to
				// be the loop footer, to make more compact asm. Thus, we didn't
				// use a `while` loop.
				//
				// See also, https://stackoverflow.com/q/47783926

				ref var entry = ref U.Add(ref entries_r0, i);
				FieldVal? fval = entry.Override;
				if (fval == null) {
					if (entry.OrigValue.Length > 0) break;
					else continue;
				} else {
					if (fval.TypeHint != FieldTypeHint.Null) break;
					else continue;
				}
			}

			int n = i + 1;
			Debug.Assert(n >= 0);
			// Assert that it's also useable with `Offsets` array
			Debug.Assert((uint)n <= (uint?)Offsets?.Length);
			return n;
		}

		[MethodImpl(MethodImplOptions.AggressiveOptimization)]
		[SkipLocalsInit]
		internal readonly int TrimNullFValsFromEndToStart(int end, int start) {
			// Ensure caller passed valid arguments
			Debug.Assert(start >= 0); // `start` can be `>= end` though
			Debug.Assert((uint)end <= (uint?)Entries?.Length);

			// Get a reference to avoid unnecessary range checking
			ref var entries_r0 = ref Entries.DangerousGetReference();

			// NOTE: `end` is excluded, as you'll see later below
			int i = end;

			for (; ; ) {
				if (--i < start) break;
				// ^- Better IL & asm than `while` loop equivalent. The `while`
				// loop would get inverted, with the condition at the loop's
				// footer. But, we want the `continue` statements (see below) to
				// be the loop footer, to make more compact asm. Thus, we didn't
				// use a `while` loop.
				//
				// See also, https://stackoverflow.com/q/47783926

				ref var entry = ref U.Add(ref entries_r0, i);
				FieldVal? fval = entry.Override;
				if (fval == null) {
					if (entry.OrigValue.Length > 0) break;
					else continue;
				} else {
					if (fval.TypeHint != FieldTypeHint.Null) break;
					else continue;
				}
			}

			int n = i + 1;
			Debug.Assert(n >= start);
			// Assert that it's also useable with `Offsets` array
			Debug.Assert((uint)n <= (uint?)Offsets?.Length);
			return n;
		}

		// --

		[DoesNotReturn]
		private static T E_FieldValsLengthTooLarge<T>(uint currentSize) {
			throw new InvalidOperationException(
				$"Total number of bytes for fields data " +
				$"{(currentSize <= MaxFieldValsLength ? "" : $"(currently {currentSize}) ")}" +
				$"exceeded the limit of {MaxFieldValsLength} bytes.");
		}
	}

	private protected struct FieldsWriter {

		internal record struct Entry(
			FieldVal? Override, LatentFieldVal OrigValue
		);

		internal int[] _Offsets;
		internal Entry[] _Entries;

		internal int _HotStoreLength, _ColdStoreLength; // -1 if should skip
		internal FieldsDesc _HotFieldsDesc, _ColdFieldsDesc;

		internal List<(StringKey Name, FieldVal Value)>? _FloatingFields;

		public readonly int HotStoreLength => _HotStoreLength;
		public readonly int ColdStoreLength => _ColdStoreLength;

		public readonly List<(StringKey Name, FieldVal Value)>? FloatingFields => _FloatingFields;

		[MethodImpl(MethodImplOptions.AggressiveOptimization)]
		[SkipLocalsInit]
		public readonly void WriteHotStore(Stream destination) {
#if DEBUG
			Debug.Assert(_HotStoreLength > 0, $"Shouldn't be called while data length <= 0");
			long expectedEndPos = destination.Position + _HotStoreLength;
#endif
			FieldsDesc fDesc = _HotFieldsDesc;
			destination.WriteVarInt(fDesc);

			int fCount = fDesc.FieldCount;
			Debug.Assert(fCount <= _Offsets?.Length);
			Debug.Assert(fCount <= _Entries?.Length);

			// Get a reference to avoid unnecessary range checking
			ref int offsets_r0 = ref _Offsets.DangerousGetReference();
			{
				int fOffsetSize = fDesc.FOffsetSize;
				for (int i = 0; i < fCount; i++) {
					destination.WriteUInt32AsUIntX(
						(uint)U.Add(ref offsets_r0, i),
						fOffsetSize);
				}
			}

			// Get a reference to avoid unnecessary range checking
			ref var entries_r0 = ref _Entries.DangerousGetReference();
			for (int i = 0; i < fCount; i++) {
				ref var entry = ref U.Add(ref entries_r0, i);
				FieldVal? fval = entry.Override;
				if (fval == null) {
					entry.OrigValue.WriteTo(destination);
				} else {
					fval.WriteTo(destination);
				}
			}

#if DEBUG
			Debug.Assert(destination.Position == expectedEndPos);
#endif
		}

		[MethodImpl(MethodImplOptions.AggressiveOptimization)]
		[SkipLocalsInit]
		public readonly void WriteColdStore(Stream destination) {
#if DEBUG
			Debug.Assert(_ColdStoreLength > 0, $"Shouldn't be called while data length <= 0");
			long expectedEndPos = destination.Position + _ColdStoreLength;
#endif
			FieldsDesc fDesc = _ColdFieldsDesc;
			destination.WriteVarInt(fDesc);

			int start = _HotFieldsDesc.FieldCount;
			int fCount = fDesc.FieldCount;
			Debug.Assert(start + fCount <= _Offsets?.Length);
			Debug.Assert(start + fCount <= _Entries?.Length);

			// Get a reference to avoid unnecessary range checking
			ref int offsets_r0 = ref _Offsets.DangerousGetReferenceAt(start);
			{
				int i = 0;
				if (i < fCount) {
					int offsetAdjustment = offsets_r0;
					int fOffsetSize = fDesc.FOffsetSize;
					do {
						destination.WriteUInt32AsUIntX(
							(uint)(U.Add(ref offsets_r0, i) - offsetAdjustment),
							fOffsetSize);
					} while (++i < fCount);
				}
			}

			// Get a reference to avoid unnecessary range checking
			ref var entries_r0 = ref _Entries.DangerousGetReferenceAt(start);
			for (int i = 0; i < fCount; i++) {
				ref var entry = ref U.Add(ref entries_r0, i);
				FieldVal? fval = entry.Override;
				if (fval == null) {
					entry.OrigValue.WriteTo(destination);
				} else {
					fval.WriteTo(destination);
				}
			}

#if DEBUG
			Debug.Assert(destination.Position == expectedEndPos);
#endif
		}

		[MethodImpl(MethodImplOptions.AggressiveOptimization)] // Use fully optimizing JIT, right from the start!
		public void Dispose() {
			var entries = _Entries;
			if (entries != null) {
				ArrayPool<Entry>.Shared.ReturnClearingReferences(entries);
				_Entries = null!;

				ArrayPool<int>.Shared.ReturnClearingReferences(_Offsets);
				_Offsets = null!;
			}
		}
	}
}
