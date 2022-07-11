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

			if ((uint)nextOffset <= (uint)MaxFieldValsLength) {
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
			Debug.Assert(_HotStoreLength > 0, $"Shouldn't be called while data length <= 0 (currently {_HotStoreLength})");
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
			Debug.Assert(_ColdStoreLength > 0, $"Shouldn't be called while data length <= 0 (currently {_ColdStoreLength})");
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

	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	[SkipLocalsInit]
	private protected void CompileFieldChanges(ref FieldsReader fr, int hotStoreLimit, ref FieldsWriter fw) {
		Dictionary<StringKey, FieldVal>? fchanges = _FieldChanges;
		if (fchanges == null) goto NoCoreFieldChanges;

		var fchanges_iter = fchanges.GetEnumerator();
		if (!fchanges_iter.MoveNext()) goto NoCoreFieldChanges;

		FieldsWriterCore fwc;
		int foverrides_n_max = fchanges.Count + 1; // Extra 1 for sentinel value

		using (BufferRenter<(FieldSpec FSpec, FieldVal FVal)>.Create(foverrides_n_max, out fwc.FOverrides)) {
			// Get a reference to avoid unnecessary range checking
			ref var foverrides_r0 = ref fwc.FOverrides.DangerousGetReference();
			int foverrides_n = 0;

			// Convert the field changes into pairs of field spec and value
			var db = fr.Db;
			using (var cmd = db.CreateCommand()) {
				SqliteParameter cmd_fld;
				cmd.Set("""
					SELECT idx_a_sto FROM SchemaToField
					WHERE (schema,fld)=($schema,$fld)
					"""
				).AddParams(
					new("$schema", _SchemaRowId),
					cmd_fld = new() { ParameterName = "$fld" }
				);

				do {
					var (fname, fval) = fchanges_iter.Current;
					cmd_fld.Value = db.LoadFieldId(fname);

					using var r = cmd.ExecuteReader();
					if (r.Read()) {
						// Case: local field

						r.DAssert_Name(0, "idx_a_sto");
						FieldSpec fspec = r.GetInt32(0);

						Debug.Assert(fspec.Index >= 0);
						fspec.StoreType.DAssert_Defined();

						// TODO-XXX If there are any schema field changes, end here and perform a schema rewrite instead

						Debug.Assert((uint)foverrides_n < (uint)foverrides_n_max);
						U.Add(ref foverrides_r0, foverrides_n++) = (fspec, fval);
					} else {
						// Case: floating field -- a field not defined by the schema

						var floatingFields = fw._FloatingFields;
						if (floatingFields == null) {
							// This becomes a conditional jump forward to not favor it
							goto InitFloatingFields;
						}

					AddFloatingField:
						floatingFields.Add((fname, fval));
						goto DoneFloatingField;

					InitFloatingFields:
						fw._FloatingFields = floatingFields = new();
						goto AddFloatingField;

					DoneFloatingField:
						;
					}
				} while (fchanges_iter.MoveNext());
			}

			// -=-

			Debug.Assert((uint)foverrides_n < (uint)foverrides_n_max);

			if (foverrides_n != 0) {
				// Order by field spec
				MemoryMarshal.CreateSpan(ref foverrides_r0, foverrides_n)
					.Sort(static (a, b) => a.FSpec.Value.CompareTo(b.FSpec.Value));

				// Set up sentinel value
				ref var foverrides_r_sentinel = ref U.Add(ref foverrides_r0, foverrides_n);
				foverrides_r_sentinel = (-1, null!);

				// Assert that the sentinel won't match any field spec
				Debug.Assert((uint)foverrides_r_sentinel.FSpec.Index >= (uint)MaxFieldCount);
			} else {
				// It's important that we don't proceed any further if we have
				// an empty list of field changes (as the code after assumes
				// that we have at least 1 field change).

				// This becomes a conditional jump forward to not favor it
				goto NoCoreFieldChanges_0;
			}

			// -=-
			//
			// Legend:
			//
			// `fmi` -- first modification index; the index of the first field change
			// `lmi` -- last modification index; the index of the last field change
			// `lmn` -- last modification end; `lmi + 1`
			//
			// `ohc` -- old hot count; the number of fields in the old hot store
			// `occ` -- old cold count; the number of fields in the old cold store
			// `olc` -- old local count; `ohc + occ`
			//
			// `xhc` -- expected maximum hot count, as defined by the schema
			// `xlc` -- expected maximum local count, as defined by the schema
			//
			// `ldn` -- 1 + the index of the last field entry loaded
			//
			//
			// As to why we're preferring shorter variable names, here's a good
			// rationale from, https://math.stackexchange.com/q/24241/#comment52289_24241
			//
			//   > Because it's long, it makes it hard to see patterns, and it
			//   > makes you think about interpretation when you should be
			//   > thinking about form.
			//
			// Actually, the above isn't really the exact reasoning :P -- those
			// were simply the variable names while the algorithm was being
			// designed, so as to make it easier to see patterns and coalesce
			// common code paths, or something like that.
			//
			// However, the variable names were preserved here mainly because
			// verbose variables just add too much clutter. It was decided that
			// the comments should already be more than sufficient to convey the
			// intention, as the comments were necesary anyway to help make
			// sense of the algorithm's design.
			//
			// -=-
			//
			// Nomenclature:
			//
			// - "hot store" -- the hot field store :P
			// - "cold store" -- the cold field store :P
			// - "cold data" means cold fields' data, which can exist in either
			// the hot store or the cold store.
			// - "hot data" means hot fields' data, which can only exist in the
			// hot store.
			// - "hot limit" means "hot store limit" -- the maximum amount of
			// data (in bytes) imposed over the hot store.
			// - "hot zone" and "cold zone"
			//   - All hot data may exist only in the hot zone. The hot zone
			//   exists exclusively in the hot store.
			//   - All cold data may exist only in the cold zone. The cold zone
			//   may be located in either the hot store or the cold store, but
			//   never in both stores.
			//   - The cold zone can be found in the hot store when both hot and
			//   cold data are within the hot limit, i.e., all of the data fits
			//   in the hot store.
			//   - The indices that lead to the hot zone is always `< xhc`
			//   - The indices that lead to the cold zone is always `>= xhc`
			//
			// -=-

			int fmi = U.Add(ref foverrides_r0, 0).FSpec.Index;
			int lmn = U.Add(ref foverrides_r0, foverrides_n - 1).FSpec.Index + 1;

			int xlc, xhc;
			using (var cmd = db.CreateCommand()) {
				cmd.Set("""
					SELECT hfld_count,cfld_count FROM Schema
					WHERE rowid=$rowid
					"""
				).AddParams(new("$rowid", _SchemaRowId));

				using var r = cmd.ExecuteReader();
				if (r.Read()) {
					r.DAssert_Name(0, "hfld_count"); // The max hot field count
					xhc = r.GetInt32(0); // The expected max hot count

					r.DAssert_Name(1, "cfld_count"); // The max cold field count
					xlc = r.GetInt32(1) + xhc; // The expected max local count
				} else {
					xlc = xhc = 0;
				}
			}

			Debug.Assert(0 <= fmi && fmi < lmn);

			// The check below also makes sure `xlc > 0` since `0 <= fmi < lmn`
			// (implying `lmn >= 1`) and we want `lmn <= xlc` to hold or throw.
			// - Though `xlc` can be zero, we shouldn't be here then, as no
			// local field may change in that case.
			// - The check also ensures `xlc` is never negative, or we throw.
			if (lmn > xlc) {
				E_IndexBeyondLocalFieldCount(_SchemaRowId, lmn: lmn, xlc: xlc);
			}

			Debug.Assert(xlc > 0); // Assured by the above

			// The check below ensures `0 <= xhc <= xlc` presuming `xlc >= 0`.
			// - Necessary for when we finally allocate the buffers later below,
			// which would be `xlc` in length each, to be cut by `xhc`.
			if ((uint)xhc > (uint)xlc) {
				E_InvalidHotFieldCount(_SchemaRowId, xhc: xhc, xlc: xlc);
			}

			// -=-

			[DoesNotReturn]
			static void E_IndexBeyondLocalFieldCount(long schemaRowId, int lmn, int xlc) {
				Debug.Assert(lmn > xlc);
				throw new InvalidDataException(
					$"Schema (with rowid {schemaRowId}) gave a local field " +
					$"index (which is {lmn-1}) not under the expected maximum " +
					$"number of local fields it defined (which is {xlc}).");
			}

			[DoesNotReturn]
			static void E_InvalidHotFieldCount(long schemaRowId, int xhc, int xlc) {
				throw new InvalidDataException(
					$"Schema (with rowid {schemaRowId}) has {xhc} as its " +
					$"maximum hot field count while having {xlc} as its " +
					$"maximum local field count.");
			}

			// -=-

			[Conditional("DEBUG")]
			static void DInit_StoreLengthsAndFDescs(ref FieldsWriter fw) {
				fw._ColdStoreLength = fw._HotStoreLength = -2;
				fw._ColdFieldsDesc = fw._HotFieldsDesc = -1;
			}

			[Conditional("DEBUG")]
			static void DAssert_StoreLengthsAndFDescs(ref FieldsWriter fw) {
				Debug.Assert(fw._HotStoreLength >= -1);
				Debug.Assert(fw._ColdStoreLength >= -1);

				// Necessary for the corrrectness of the code after
				Debug.Assert(FieldsDesc.MaxValue == int.MaxValue);

				Debug.Assert(
					(fw._HotStoreLength <= 0 && fw._ColdStoreLength <= 0) ||
					(uint)fw._HotFieldsDesc <= (uint)FieldsDesc.MaxValue
				);
				Debug.Assert(
					fw._ColdStoreLength <= 0 ||
					(uint)fw._ColdFieldsDesc <= (uint)FieldsDesc.MaxValue
				);
			}

			// -=-

			DInit_StoreLengthsAndFDescs(ref fw);

			Debug.Assert(fwc.FOverrides != null); // Already assigned before
			fw._Entries = fwc.Entries = ArrayPool<FieldsWriter.Entry>.Shared.Rent(xlc);
			ref var offsets_r0 = ref (
				fw._Offsets = fwc.Offsets = ArrayPool<int>.Shared.Rent(xlc)
			).DangerousGetReference();

			int ohc = fr.HotFieldCount;
			Debug.Assert(ohc >= 0); // Code below assumes this

			int ldn;
			int fValsSize;
			int hotFValsSize;

			if (!fr.HasRealColdStore) {
				// Case: No real cold store (at least according to the flag)

				ldn = Math.Max(lmn, ohc);

				// This becomes a conditional jump forward to not favor it
				if (ldn > MaxFieldCount) goto Load__E_TooManyFields;

				fValsSize = fwc.LoadHot(ref fr, end: ldn);
				ldn = fwc.TrimNullFValsFromEnd(end: ldn);

				if (fValsSize <= hotStoreLimit || ldn <= xhc) {
					// Case: Either within the hot limit or no cold data

					// Leave old cold store as is. No real cold store anyway.
					fw._ColdStoreLength = -1;

					if (ldn != 0) {
						// Case: Stil got fields loaded
						goto RewriteHotOnly_HotLoaded_NoCold;
					} else
						goto ClearHotOnly_NoCold;

				} else {
					// Case: Beyond the hot limit with cold data
					Debug.Assert(fValsSize > hotStoreLimit && ldn > xhc);

					hotFValsSize = U.Add(ref offsets_r0, xhc);
					goto RewriteHotColdSplit_ColdLoaded;
				}
			} else if (xhc == ohc) {
				// Case: Has real cold store, with hot store uncorrupted.
				// - It should be that `xhc == ohc` whenever the "has real cold
				// store" flag is set in the hot store. Otherwise, the hot store
				// is considered corrupted.

				if (lmn <= xhc) {
					// Case: All changes are in the hot zone only

				} else if (xhc <= fmi) {
					// Case: All changes are in the cold zone only

				} else {
					// Case: Changes in both hot and cold zones

				}
			} else {
				// Case: Has real cold store, but hot store is corrupted.
				// - Expecting `xhc == ohc` whenever the "has real cold store"
				// flag is set in the hot store. But this isn't the case.
				// - A full rewrite should fix the issue.

			}

		Done:
			DAssert_StoreLengthsAndFDescs(ref fw);
			return; // ---

		RewriteHotOnly_HotLoaded_NoCold:
			{
				Debug.Assert(fw._ColdStoreLength == (fr.HasRealColdStore ? 0 : -1));
				Debug.Assert(ldn > 0, $"Needs at least 1 field loaded");

				int hotFOffsetSizeM1Or0 = (
					(uint)U.Add(ref offsets_r0, ldn-1)
				).CountBytesNeededM1Or0();

				FieldsDesc hotFDesc = new(
					fCount: ldn,
					fOffsetSizeM1Or0: hotFOffsetSizeM1Or0
				);

				fw._HotFieldsDesc = hotFDesc;
				fw._HotStoreLength = VarInts.Length(hotFDesc)
					+ ldn * (hotFOffsetSizeM1Or0 + 1)
					+ fValsSize;

				goto Done;
			}

		RewriteHotOnly_HotLoadedFull_HasCold:
			{
				Debug.Assert(fw._ColdStoreLength == -1);
				Debug.Assert(xhc == ohc && fr.HasRealColdStore);
				Debug.Assert(xhc > 0, $"Needs at least 1 field in the hot zone");

				goto Done;
			}

		RewriteColdOnly_ColdLoaded_HasCold:
			{
				Debug.Assert(fw._HotStoreLength == -1);
				Debug.Assert(xhc == ohc && fr.HasRealColdStore);
				Debug.Assert(ldn - xhc > 0, $"Needs at least 1 cold field loaded");

				goto Done;
			}

		RewriteHotColdSplit_ColdLoaded:
			{
				Debug.Assert(ldn - xhc > 0, $"Needs at least 1 cold field loaded");

				goto Done;
			}

		ClearHotOnly_NoCold:
			{
				Debug.Assert(fw._ColdStoreLength == (fr.HasRealColdStore ? 0 : -1));

				fw._HotFieldsDesc = FieldsDesc.Empty;
				fw._HotStoreLength = FieldsDesc.VarIntLengthForEmpty;

				goto Done;
			}

		NoCoreFieldChanges_0:
			// ^ Label must still be within the `using` block, so that the
			// `goto` statement jumping into this can simply be a direct jump or
			// a conditional jump forward, while the `goto` here will take care
			// of the elaborate step of actually leaving the `using` block.
			goto NoCoreFieldChanges;

		Load__E_TooManyFields:
			E_TooManyFields(this, ldn);

			[DoesNotReturn]
			static void E_TooManyFields(FieldedEntity entity, int currentCount) {
				Debug.Assert(currentCount > MaxFieldCount);
				throw new InvalidOperationException(
					$"Total number of fields (currently {currentCount}) " +
					$"shouldn't exceed {MaxFieldCount};" +
					$"{Environment.NewLine}Entity: {entity.GetDebugLabel()};" +
					$"{Environment.NewLine}Schema: {entity._SchemaRowId};");
			}
		}

	NoCoreFieldChanges:
		fw._ColdStoreLength = fw._HotStoreLength = -1;
	}
}
