namespace Kokoro.Internal;
using Kokoro.Common.Util;
using Kokoro.Internal.Sqlite;
using Microsoft.Data.Sqlite;
using System.Buffers;

partial class FieldedEntity {
	// Ideally, less than the default page size of SQLite version 3.12.0
	// (2016-03-29), which is 4096.
	internal const int DefaultHotStoreLimit = 3328;

	internal const int MaxFieldCount = byte.MaxValue;
	internal const int MaxFieldValsLength = 0xFF_FFFF;

	private protected struct FieldsWriter {

		internal int[] _Offsets;
		internal FieldVal?[] _Entries;

		internal int _HotStoreLength, _ColdStoreLength; // -1 if should skip
		internal FieldsDesc _HotFieldsDesc, _ColdFieldsDesc;

		internal List<(long Id, FieldVal Entry)>? _FloatingFields;

		public readonly int HotStoreLength => _HotStoreLength;
		public readonly int ColdStoreLength => _ColdStoreLength;

		public readonly List<(long Id, FieldVal Entry)>? FloatingFields => _FloatingFields;

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

			Debug.Assert(_Offsets != null);
			Debug.Assert(_Entries != null);

			Debug.Assert(fCount <= _Offsets.Length);
			Debug.Assert(fCount <= _Entries.Length);

			// Get a reference to avoid unnecessary range checking
			ref int offsets_r0 = ref _Offsets.DangerousGetReference();
			{
				// NOTE: The first offset value is never stored, as it'll always
				// be zero otherwise.
				Debug.Assert(fCount <= 0 || offsets_r0 == 0);

				int i = 1; // Skips to the second offset value
				if (i < fCount) {
					int fOffsetSize = fDesc.FOffsetSize;
					do {
						destination.WriteUInt32AsUIntX(
							(uint)U.Add(ref offsets_r0, i),
							fOffsetSize);
					} while (++i < fCount);
				}
			}

			// Get a reference to avoid unnecessary range checking
			ref var entries_r0 = ref _Entries.DangerousGetReference();
			for (int i = 0; i < fCount; i++) {
				FieldVal? entry = U.Add(ref entries_r0, i);
				Debug.Assert(entry != null, $"Unexpected null entry at {i}");
				entry.WriteTo(destination);
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

			Debug.Assert(_Offsets != null);
			Debug.Assert(_Entries != null);

			Debug.Assert(start + fCount <= _Offsets.Length);
			Debug.Assert(start + fCount <= _Entries.Length);

			// Get a reference to avoid unnecessary range checking
			ref int offsets_r0 = ref _Offsets.DangerousGetReferenceAt(start);
			{
				// NOTE: The first offset value is never stored, as it'll always
				// be zero otherwise.
				Debug.Assert(fCount <= 0 || offsets_r0 == 0);

				int i = 1; // Skips to the second offset value
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
				FieldVal? entry = U.Add(ref entries_r0, i);
				Debug.Assert(entry != null, $"Unexpected null entry at {i}");
				entry.WriteTo(destination);
			}
#if DEBUG
			Debug.Assert(destination.Position == expectedEndPos);
#endif
		}

		[MethodImpl(MethodImplOptions.AggressiveOptimization)]
		internal void InitEntries(int count) {
			_Entries = ArrayPool<FieldVal?>.Shared.Rent(count);
			_Offsets = ArrayPool<int>.Shared.Rent(count);
			// ^- NOTE: Must be done last. See code for `Dispose()`
		}

		/// <remarks>
		/// The schema may change without rewriting the fields data, this can
		/// happen either due to data corruption or during a `<see cref="Prot.Schema">Schema</see>.usum`
		/// collision. If the latter, it might be better to keep any excess
		/// fields (instead of discarding them), in case the original schema
		/// will/might be reinstated later.
		/// <para>
		/// This helper method is useful in such scenarios, for when the buffers
		/// have already been allocated but must be resized to accommodate
		/// unexpected excess field entries.
		/// </para>
		/// </remarks>
		/// <seealso cref="ReInitEntriesWithCheck(FieldedEntity, int)"/>
		internal void ReInitEntries(int count) {
			DeInitEntries();
			InitEntries(count);
		}

		/// <summary>
		/// Similar to <see cref="ReInitEntries(int)"/> but also makes sure <paramref name="count"/>
		/// doesn't exceed <see cref="MaxFieldCount"/>.
		/// </summary>
		internal void ReInitEntriesWithCheck(FieldedEntity owner, int count) {
			if (count <= MaxFieldCount) {
				ReInitEntries(count);
			} else {
				owner.E_TooManyFields(count);
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveOptimization)]
		[SkipLocalsInit]
		internal void DeInitEntries() {
			var offsets = _Offsets;
			if (offsets != null) {
				ArrayPool<int>.Shared.Return(offsets, clearArray: false);
				_Offsets = null!;

				ArrayPool<FieldVal?>.Shared.ReturnClearingReferences(_Entries);
				_Entries = null!;
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Dispose() {
			// NOTE: Client code should ensure that we're always disposed, even
			// when rewriting fails (i.e., even when an exception occurs) and
			// the buffers were not initialized.
			DeInitEntries();
		}

		// --

		[MethodImpl(MethodImplOptions.AggressiveOptimization)]
		[SkipLocalsInit]
		internal readonly int Load(ref FieldsReader fr, int nextOffset, int start, int end) {
			try {
				Debug.Assert((uint)end <= (uint)MaxFieldCount);

				Debug.Assert((uint)end <= (uint?)_Offsets?.Length); // `false` on null array
				Debug.Assert((uint)end <= (uint?)_Entries?.Length);

				// Get references to avoid unnecessary range checking
				ref var entries_r0 = ref _Entries.DangerousGetReference();
				ref var offsets_r0 = ref _Offsets.DangerousGetReference();

				for (int i = start; i < end; i++) {
					U.Add(ref offsets_r0, i) = nextOffset;
					ref var entry = ref U.Add(ref entries_r0, i);

					FieldVal? fval = entry;
					if (fval == null) {
						FieldSpec fspec = new(i, FieldStoreType.Cold);
						fval = fr.Read(fspec);
						entry = fval;
					}

					checked {
						nextOffset += (int)fval.CountEncodeLength();
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


		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[SkipLocalsInit]
		internal readonly int TrimNullFValsFromEnd(int end) {
			int n = TrimNullFValsFromEnd(_Entries, end);
			// Assert that it's also useable with `Offsets` array
			Debug.Assert((uint)n <= (uint?)_Offsets?.Length);
			return n;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[SkipLocalsInit]
		internal readonly int TrimNullFValsFromEndToStart(int end, int start) {
			int n = TrimNullFValsFromEndToStart(_Entries, end, start);
			// Assert that it's also useable with `Offsets` array
			Debug.Assert((uint)n <= (uint?)_Offsets?.Length);
			return n;
		}


		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[SkipLocalsInit]
		internal static int TrimNullFValsFromEnd(FieldVal?[] entries, int end) {
			// Ensure caller passed valid arguments
			Debug.Assert((uint)end <= (uint?)entries?.Length);
			// Method operates on reference to avoid unnecessary range checking
			ref var entries_r0 = ref entries.DangerousGetReference();
			return TrimNullFValsFromEnd(ref entries_r0, end);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[SkipLocalsInit]
		internal static int TrimNullFValsFromEndToStart(FieldVal?[] entries, int end, int start) {
			// Ensure caller passed valid arguments
			Debug.Assert(start >= 0); // `start` can be `>= end` though
			Debug.Assert((uint)end <= (uint?)entries?.Length);
			// Method operates on reference to avoid unnecessary range checking
			ref var entries_r0 = ref entries.DangerousGetReference();
			return TrimNullFValsFromEndToStart(ref entries_r0, end, start);
		}


		[MethodImpl(MethodImplOptions.AggressiveOptimization)]
		[SkipLocalsInit]
		internal static int TrimNullFValsFromEnd(ref FieldVal? entries_r0, int end) {
			// NOTE: `end` is excluded, as you'll see later below
			int i = end;

			for (; ; ) {
				if (--i < 0) break;

				FieldVal? fval = U.Add(ref entries_r0, i);
				Debug.Assert(fval != null);

				if (fval.TypeHint != FieldTypeHint.Null) break;
				else continue; // See also, https://stackoverflow.com/q/47783926
			}

			int n = i + 1;
			Debug.Assert(n >= 0);
			return n;
		}

		[MethodImpl(MethodImplOptions.AggressiveOptimization)]
		[SkipLocalsInit]
		internal static int TrimNullFValsFromEndToStart(ref FieldVal? entries_r0, int end, int start) {
			// NOTE: `end` is excluded, as you'll see later below
			int i = end;

			for (; ; ) {
				if (--i < start) break;

				FieldVal? fval = U.Add(ref entries_r0, i);
				Debug.Assert(fval != null);

				if (fval.TypeHint != FieldTypeHint.Null) break;
				else continue; // See also, https://stackoverflow.com/q/47783926
			}

			int n = i + 1;
			Debug.Assert(n >= start);
			return n;
		}

		// --

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal bool HasEntryMissing(int start, int end)
			=> HasEntryMissing(_Entries, start, end);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[SkipLocalsInit]
		internal static bool HasEntryMissing(FieldVal?[] entries, int start, int end) {
			// Ensure caller passed valid arguments
			Debug.Assert((uint)start <= (uint)end);
			Debug.Assert((uint)end <= (uint?)entries?.Length);
			// Method operates on reference to avoid unnecessary range checking
			ref var entries_r0 = ref entries.DangerousGetReference();
			return HasEntryMissing(ref entries_r0, start, end);
		}

		[SkipLocalsInit]
		internal static bool HasEntryMissing(ref FieldVal? entries_r0, int start, int end) {
			for (int i = start; i < end; i++) {
				if (U.Add(ref entries_r0, i) == null) {
					return true;
				}
			}
			return false;
		}
	}

	[DoesNotReturn]
	private static void E_FieldValsLengthTooLarge(uint currentSize)
		=> E_FieldValsLengthTooLarge<int>(currentSize);

	[DoesNotReturn]
	private static T E_FieldValsLengthTooLarge<T>(uint currentSize) {
		throw new InvalidOperationException(
			$"Total number of bytes for fields data " +
			$"{(currentSize <= MaxFieldValsLength ? "" : $"(currently {currentSize}) ")}" +
			$"exceeded the limit of {MaxFieldValsLength} bytes.");
	}

	// --

	[Conditional("DEBUG")]
	private static void DAssert_FieldsWriterPriorRewrite(ref FieldsWriter fw) {
		Debug.Assert(fw._Offsets == null,
			$"`{nameof(fw._Offsets)}` must be null prior a fields rewrite");

		Debug.Assert(fw._Entries == null,
			$"`{nameof(fw._Entries)}` must be null prior a fields rewrite");

		Debug.Assert(fw._FloatingFields is null or { Count: 0 },
			$"`{nameof(fw._FloatingFields)}` must be null or empty prior a fields rewrite");
	}

	[Conditional("DEBUG")]
	private static void DInit_StoreLengthsAndFDescs(ref FieldsWriter fw) {
		fw._ColdStoreLength = fw._HotStoreLength = -2;
		fw._ColdFieldsDesc = fw._HotFieldsDesc = -1;
	}

	[Conditional("DEBUG")]
	private static void DAssert_FieldsWriterAfterRewrite(ref FieldsWriter fw) {
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

		Debug.Assert(
			fw._ColdStoreLength != 0 ||
			!fw._HotFieldsDesc.HasColdComplement,
			$"Hot store flag `{nameof(FieldsDesc.HasColdComplement)}` should " +
			$"not be set if cold store length is zero"
		);

		Debug.Assert(
			fw._ColdStoreLength <= 0 ||
			!fw._ColdFieldsDesc.HasColdComplement,
			$"Cold store flag `{nameof(FieldsDesc.HasColdComplement)}` should" +
			$" never be set if cold store data will be present on rewrite"
		);

		Debug.Assert(
			(fw._HotStoreLength <= 0 && fw._ColdStoreLength <= 0) ||
			fw._Offsets != null
		);
		Debug.Assert(
			fw._Offsets == null == (fw._Entries == null)
		);
	}

	// --

	private protected bool MayCompileFieldChanges {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[SkipLocalsInit]
		get {
			var fields = _Fields;
			if (fields != null && fields.Changes != null)
				return true;
			return false;
		}
	}

	/// <remarks>
	/// <para>
	/// NOTE: This method may modify <see cref="_SchemaId"/> on successful
	/// return. If it's important that the old value of <see cref="_SchemaId"/>
	/// be preserved, perform a manual backup of the old value before the call.
	/// </para>
	/// CONTRACT:
	/// <br/>- Must be called while inside a transaction (ideally, using <see cref="NestingWriteTransaction"/>).
	/// <br/>- Must load <see cref="_SchemaId"/> beforehand, at least once,
	/// while inside the transaction.
	/// <br/>- Must load <see cref="_SchemaId"/> with the rowid of the actual
	/// schema being used by the <see cref="FieldedEntity">fielded entity</see>.
	/// <br/>- Must be called while <see cref="MayCompileFieldChanges"/> is <see langword="true"/>.
	/// <para>
	/// Violation of the above contract may result in undefined behavior.
	/// </para>
	/// </remarks>
	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	[SkipLocalsInit]
	private protected void CompileFieldChanges(ref FieldsReader fr, ref FieldsWriter fw, int hotStoreLimit = DefaultHotStoreLimit) {
		DAssert_FieldsWriterPriorRewrite(ref fw);

		Fields? fields = _Fields;
		Debug.Assert(fields != null, $"`{nameof(_Fields)}` must be non-null " +
			$"prior to calling this method (see also, `{nameof(MayCompileFieldChanges)}`)");

		FieldChanges? fchanges = fields.Changes;
		Debug.Assert(fchanges != null, $"`{nameof(_Fields)}.{nameof(_Fields.Changes)}` must be non-null " +
			$"prior to calling this method (see also, `{nameof(MayCompileFieldChanges)}`)");

		var fchanges_iter = fchanges.GetEnumerator();
		if (!fchanges_iter.MoveNext()) goto NoFieldChanges;

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

		int xlc, xhc;

		var db = fr.Db;
		using (var cmd = db.CreateCommand()) {
			cmd.Set(
				$"SELECT hotCount,coldCount FROM {Prot.Schema}\n" +
				$"WHERE rowid=$rowid"
			).AddParams(new("$rowid", _SchemaId));

			using var r = cmd.ExecuteReader();
			if (r.Read()) {
				r.DAssert_Name(0, "hotCount"); // The max hot field count
				xhc = r.GetInt32(0); // The expected max hot count

				r.DAssert_Name(1, "coldCount"); // The max cold field count
				xlc = r.GetInt32(1) + xhc; // The expected max local count
			} else {
				xlc = xhc = 0;
			}
		}

		// The check below ensures `0 <= xhc <= xlc <= MaxFieldCount`
		// - Necessary for when we finally allocate the "entries" buffer later
		// below, which would be `xlc` in length, to be cut by `xhc`.
		if ((uint)xlc > (uint)MaxFieldCount || (uint)xhc > (uint)xlc) {
			E_InvalidLocalFieldCounts_InvDat(_SchemaId, xhc: xhc, xlc: xlc);
		}

		[DoesNotReturn]
		static void E_InvalidLocalFieldCounts_InvDat(long schemaId, int xhc, int xlc) {
			throw new InvalidDataException(
				$"Schema (with rowid {schemaId}) has {xhc} as its maximum " +
				$"hot field count while having {xlc} as its maximum local " +
				$"field count" + (xlc <= MaxFieldCount ? $"." : $" (which " +
				$"exceeds {MaxFieldCount}, the maximum allowed count)."));
		}

		// -=-

		fw.InitEntries(xlc);

		int fmi = -1; Debug.Assert(uint.MaxValue == unchecked((uint)-1));
		int lmi = 0;

		using (var cmd = db.CreateCommand()) {
			SqliteParameter cmd_fld;
			cmd.Set(
				$"SELECT idx_sto FROM {Prot.SchemaToField}\n" +
				$"WHERE (schema,fld)=($schema,$fld)"
			).AddParams(
				new("$schema", _SchemaId),
				cmd_fld = new() { ParameterName = "$fld" }
			);

			db.ReloadNameIdCaches(); // Needed by `db.LoadStale…()` below
			do {
				var (fname, fchg) = fchanges_iter.Current;

				FieldVal fval;
				if (fchg.GetType() != typeof(StringKey)) {
					Debug.Assert(fchg is FieldVal);
					fval = U.As<FieldVal>(fchg);
				} else {
					Debug.Assert(fchg is StringKey);
					var fsrc = U.As<StringKey>(fchg);

					long fld2 = db.LoadStaleNameId(fsrc);
					cmd_fld.Value = fld2;

					using var r = cmd.ExecuteReader();
					if (r.Read()) {
						r.DAssert_Name(0, "idx_sto");
						FieldSpec fspec2 = r.GetInt32(0);
						fspec2.DAssert_Valid();

						fval = fr.Read(fspec2);
					} else {
						fval = OnLoadFloatingField(db, fld2) ?? FieldVal.Null;
					}
				}

				long fld = db.LoadStaleOrEnsureNameId(fname);
				cmd_fld.Value = fld;

				using (var r = cmd.ExecuteReader()) {
					if (r.Read()) {
						// Case: Core field

						r.DAssert_Name(0, "idx_sto");
						FieldSpec fspec = r.GetInt32(0);
						fspec.DAssert_Valid();

						if (fspec.StoreType != FieldStoreType.Shared) {
							int i = fspec.Index;

							if ((uint)i < (uint)fmi) fmi = i;
							if ((uint)i > (uint)lmi) lmi = i;

							if ((uint)i < (uint)xlc) {
								Debug.Assert((uint)xlc <= (uint)fw._Entries.Length);
								fw._Entries.DangerousGetReferenceAt(i) = fval;
							} else {
								E_IndexBeyondLocalFieldCount_InvDat(_SchemaId, i, xlc: xlc);
							}
						} else {
							// If there are any schema field changes, end here
							// and perform a schema rewrite instead.
							goto RewriteSchema;
							// ^- NOTE: We'll perform the schema rewrite outside
							// the scope of any `using` statement, in order to
							// first dispose any guarded disposable, before
							// proceeding with the planned operation.
						}
					} else {
						// Case: Floating field

						var floatingFields = fw._FloatingFields;
						if (floatingFields == null) {
							// This becomes a conditional jump forward to not favor it
							goto InitFloatingFields;
						}

					AddFloatingField:
						floatingFields.Add((fld, fval));
						goto DoneFloatingField;

					InitFloatingFields:
						fw._FloatingFields = floatingFields = new();
						goto AddFloatingField;

					DoneFloatingField:
						;
					}
				}
			} while (fchanges_iter.MoveNext());
		}

		if (fmi < 0) {
			// It's important that we don't proceed any further if we have an
			// empty set of core field changes (as the code after assumes that
			// we have at least 1 core field change).
			goto NoCoreFieldChanges;
		}

		Debug.Assert(fmi <= lmi);
		Debug.Assert(lmi < xlc);

		// -=-

		DInit_StoreLengthsAndFDescs(ref fw);

		int ohc = fr.HotFieldCount;
		Debug.Assert(ohc >= 0); // Code below assumes this

		int ldn;
		int fValsSize;
		int hotFValsSize;

		if (!fr.HasRealColdStore) {
			// Case: No real cold store (at least according to the flag)

			if (ohc > xlc) goto GrowEntries_ohc; GrowEntries_Done:;
			ldn = Math.Max(lmi+1, ohc);

			fValsSize = fw.LoadHot(ref fr, end: ldn);
			ldn = fw.TrimNullFValsFromEnd(end: ldn);

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

				hotFValsSize = fw._Offsets.DangerousGetReferenceAt(xhc);
				goto RewriteHotColdSplit_ColdLoaded;
			}

			Debug.Fail("This point should be unreachable.");

		GrowEntries_ohc:
			fw.ReInitEntriesWithCheck(this, ohc);
			goto GrowEntries_Done;

		} else if (xhc == ohc) {
			// Case: Has real cold store (at least according to the flag), with
			// hot store uncorrupted.
			// - It should be that `xhc == ohc` whenever the "has real cold
			// store" flag is set in the hot store. Otherwise, the hot store is
			// considered corrupted.

			if (lmi < xhc) {
				// Case: All changes are in the hot zone only

				// Leave old cold store as is. Don't rewrite it.
				fw._ColdStoreLength = -1;

				// Rewrite hot store only, without trimming null fields
				fValsSize = fw.LoadHot(ref fr, end: xhc);

				goto RewriteHotOnly_HotLoadedFull_HasCold;

			} else if (xhc <= fmi) {
				// Case: All changes are in the cold zone only

				fr.InitColdStore();

				Debug.Assert(xhc == ohc); // Future-proofing
				int olc = xhc + fr.ColdFieldCountOrUND;
				if (olc > xlc) goto GrowEntries_olc; GrowEntries_Done:;
				ldn = Math.Max(lmi+1, olc);

				// Load only cold fields (for now)
				fValsSize = fw.Load(ref fr,
					nextOffset: hotFValsSize = fr.HotFieldValsLength,
					start: xhc, end: ldn);

				if (fValsSize > hotStoreLimit) {
					// Case: Beyond the hot limit

					ldn = fw.TrimNullFValsFromEndToStart(end: ldn, start: xhc);
					Debug.Assert(ldn >= xhc);

					if (ldn != xhc) {
						// Case: Still got cold fields loaded

						// Leave old hot store as is. Don't rewrite it.
						fw._HotStoreLength = -1;

						goto RewriteColdOnly_ColdLoaded_HasCold;
					} else {
						// Case: No cold fields at all
						// - Perhaps all were null fields and got cleared
						Debug.Assert(ldn == xhc); // Future-proofing
						goto LoadHotOnPartialLoad_ClearCold_TryRewriteHot;
					}
				} else {
					// Case: Within the hot limit
					goto LoadHotOnPartialLoad_ClearCold_TryRewriteHot;
				}

				Debug.Fail("This point should be unreachable.");

			GrowEntries_olc:
				fw.ReInitEntriesWithCheck(this, olc);
				goto GrowEntries_Done;

			} else {
				// Case: Changes in both hot and cold zones
				Debug.Assert(lmi >= xhc && xhc > fmi);

				goto LoadAll_TryRewriteHotColdSplit;
			}
		} else {
			// Case: Has real cold store (at least according to the flag), but
			// hot store is corrupted.
			// - Expecting `xhc == ohc` whenever the "has real cold store" flag
			// is set in the hot store. But this isn't the case.
			// - A full rewrite should fix the issue.

			Debug.Assert(xhc != ohc); // Future-proofing

			Debug.Fail(
				$"Invalid: `xhc != ohc` while \"real cold store\" flag set;" +
				$"{Environment.NewLine}Entity: {GetDebugLabel()};" +
				$"{Environment.NewLine}Schema: {_SchemaId}; " +
				$"{Environment.NewLine}xhc={xhc}; ohc={ohc};");

			goto LoadAll_TryRewriteHotColdSplit;
		}

		Debug.Fail("This point should be unreachable.");

	LoadAll_TryRewriteHotColdSplit:
		{
			fr.InitColdStore();
			int olc = ohc + fr.ColdFieldCountOrUND;
			if (olc > xlc) goto GrowEntries_olc; GrowEntries_Done:;
			ldn = Math.Max(lmi+1, olc);

			fValsSize = fw.LoadHot(ref fr, end: ldn);
			ldn = fw.TrimNullFValsFromEnd(end: ldn);

			if (fValsSize > hotStoreLimit) {
				// Case: Beyond the hot limit
				if (ldn > xhc) {
					// Case: Still got cold fields loaded
					// - This case should be the favored case, given that we're
					// doing a hot-cold split rewrite anyway.
					hotFValsSize = fw._Offsets.DangerousGetReferenceAt(xhc);
					goto RewriteHotColdSplit_ColdLoaded;
				} else {
					// Case: No cold fields at all
					// - Perhaps all were null fields and got cleared
					goto ClearCold_TryRewriteHot;
				}
			} else {
				// Case: Within the hot limit
				goto ClearCold_TryRewriteHot;
			}

			Debug.Fail("This point should be unreachable.");

		GrowEntries_olc:
			fw.ReInitEntriesWithCheck(this, olc);
			goto GrowEntries_Done;
		}

	LoadHotOnPartialLoad_ClearCold_TryRewriteHot:
		{
			// Plan: Load hot data, clear the cold store, and rewrite the hot
			// store to unset the "has real cold store" flag.

			Debug.Assert(xhc == ohc && fr.HasRealColdStore);
			// Assert that there're no field changes in the hot zone
			Debug.Assert(xhc <= fmi);
			// Prior code leading to this code path should've ensured that we'll
			// load fields within the max field count.
			Debug.Assert(xhc <= ldn && ldn <= MaxFieldCount);

			int newHotFValsSize = fw.LoadHot(ref fr, end: xhc);
			if (newHotFValsSize != hotFValsSize) {
				// Case: Hot store corrupted
				// - Expected hot data size doesn't match the actual recomputed
				// size.
				// - Cold data offsets already loaded will have to be
				// re-adjusted before we can proceed.
				//   - Reloading them should fix the issue.

				// This becomes a conditional jump forward to not favor it
				goto OnCorruptedHotFValsSize_ReloadColdData;
			}

		TrimFull_ClearCold_TryRewriteHot:
			{
				ldn = fw.TrimNullFValsFromEnd(end: ldn);
				goto ClearCold_TryRewriteHot;
			}

		OnCorruptedHotFValsSize_ReloadColdData:
			{
				Debug.Assert(newHotFValsSize < hotFValsSize,
					"Should've been true if loading logic is correctly implemented.");

				Debug.Fail(
					$"Invalid: Entity found with expected hot data size " +
					$"different from when actually loaded." +
					$"{Environment.NewLine}Entity: {GetDebugLabel()};" +
					$"{Environment.NewLine}Schema: {_SchemaId}; " +
					$"{Environment.NewLine}Expected hot data size: {hotFValsSize};" +
					$"{Environment.NewLine}Actual hot data size:   {newHotFValsSize};");

				Debug.Assert(xhc == ohc); // Future-proofing
				fValsSize = fw.Load(ref fr, nextOffset: newHotFValsSize, start: xhc, end: ldn);
				goto TrimFull_ClearCold_TryRewriteHot;
			}
		}

	Done:
		DAssert_FieldsWriterAfterRewrite(ref fw);
		return; // ---

	ClearCold_TryRewriteHot:
		{
			// Plan: Clear the cold store and rewrite the hot store (to also
			// unset the "has real cold store" flag if set).

			// Clear the old cold store
			fw._ColdStoreLength = 0;

			if (ldn != 0) {
				// Case: Stil got fields loaded
				goto RewriteHotOnly_HotLoaded_NoCold;
			} else
				goto ClearHotOnly_NoCold;
		}

	RewriteHotOnly_HotLoaded_NoCold:
		{
			Debug.Assert(fw._ColdStoreLength == (fr.HasRealColdStore ? 0 : -1));
			Debug.Assert(ldn > 0, $"Needs at least 1 field loaded");
			Debug.Assert(fValsSize >= 0);

			int hotFOffsetSizeM1Or0 = (
				(uint)fw._Offsets.DangerousGetReferenceAt(ldn-1)
			).CountBytesNeededM1Or0();

			FieldsDesc hotFDesc = new(
				fCount: ldn,
				fOffsetSizeM1Or0: hotFOffsetSizeM1Or0
			);

			fw._HotFieldsDesc = hotFDesc;

			// NOTE: The first offset value is never stored, as it'll always be
			// zero otherwise.
			fw._HotStoreLength = VarInts.Length(hotFDesc)
				+ (ldn - 1) * (hotFOffsetSizeM1Or0 + 1)
				+ fValsSize;

			goto Done;
		}

	RewriteHotOnly_HotLoadedFull_HasCold:
		{
			Debug.Assert(fw._ColdStoreLength == -1, $"Shouldn't rewrite cold store");
			Debug.Assert(xhc == ohc && fr.HasRealColdStore, $"Should have cold store with hot store uncorrupted");
			Debug.Assert(xhc > 0, $"Needs at least 1 field in the hot zone");
			Debug.Assert(fValsSize >= 0);

			int hotFOffsetSizeM1Or0 = (
				(uint)fw._Offsets.DangerousGetReferenceAt(xhc-1)
			).CountBytesNeededM1Or0();

			FieldsDesc hotFDesc = new(
				fCount: xhc,
				fHasCold: true,
				fOffsetSizeM1Or0: hotFOffsetSizeM1Or0
			);

			fw._HotFieldsDesc = hotFDesc;

			// NOTE: The first offset value is never stored, as it'll always be
			// zero otherwise.
			fw._HotStoreLength = VarInts.Length(hotFDesc)
				+ (xhc - 1) * (hotFOffsetSizeM1Or0 + 1)
				+ fValsSize;

			goto Done;
		}

	RewriteColdOnly_ColdLoaded_HasCold:
		{
			Debug.Assert(fw._HotStoreLength == -1, $"Shouldn't rewrite hot store");
			Debug.Assert(xhc == ohc && fr.HasRealColdStore, $"Should have cold store with hot store uncorrupted");
			Debug.Assert(ldn > xhc, $"Needs at least 1 cold field loaded");
			Debug.Assert(fValsSize > hotFValsSize && hotFValsSize >= 0);

			int coldFOffsetSizeM1Or0 = (
				(uint)(fw._Offsets.DangerousGetReferenceAt(ldn-1) - hotFValsSize)
			).CountBytesNeededM1Or0();

			int ncc = ldn - xhc;
			FieldsDesc coldFDesc = new(
				fCount: ncc,
				fOffsetSizeM1Or0: coldFOffsetSizeM1Or0
			);

			fw._ColdFieldsDesc = coldFDesc;

			// NOTE: The first offset value is never stored, as it'll always be
			// zero otherwise.
			fw._ColdStoreLength = VarInts.Length(coldFDesc)
				+ (ncc - 1) * (coldFOffsetSizeM1Or0 + 1)
				+ (fValsSize - hotFValsSize);

			// In this case, only `fCount` should ever be used
			fw._HotFieldsDesc = new(fCount: xhc, 0);

			goto Done;
		}

	RewriteHotColdSplit_ColdLoaded:
		{
			Debug.Assert(ldn > xhc, $"Needs at least 1 cold field loaded");
			Debug.Assert(fValsSize > hotFValsSize && hotFValsSize >= 0);

			if (xhc != 0) {
				int hotFOffsetSizeM1Or0 = (
					(uint)fw._Offsets.DangerousGetReferenceAt(xhc-1)
				).CountBytesNeededM1Or0();

				FieldsDesc hotFDesc = new(
					fCount: xhc,
					fHasCold: true,
					fOffsetSizeM1Or0: hotFOffsetSizeM1Or0
				);

				fw._HotFieldsDesc = hotFDesc;

				// NOTE: The first offset value is never stored, as it'll always
				// be zero otherwise.
				fw._HotStoreLength = VarInts.Length(hotFDesc)
					+ (xhc - 1) * (hotFOffsetSizeM1Or0 + 1)
					+ hotFValsSize;
			} else {
				fw._HotStoreLength = 0;
			}

			int coldFOffsetSizeM1Or0 = (
				(uint)(fw._Offsets.DangerousGetReferenceAt(ldn-1) - hotFValsSize)
			).CountBytesNeededM1Or0();

			int ncc = ldn - xhc;
			FieldsDesc coldFDesc = new(
				fCount: ncc,
				fOffsetSizeM1Or0: coldFOffsetSizeM1Or0
			);

			fw._ColdFieldsDesc = coldFDesc;

			// NOTE: The first offset value is never stored, as it'll always be
			// zero otherwise.
			fw._ColdStoreLength = VarInts.Length(coldFDesc)
				+ (ncc - 1) * (coldFOffsetSizeM1Or0 + 1)
				+ (fValsSize - hotFValsSize);

			goto Done;
		}

	ClearHotOnly_NoCold:
		{
			Debug.Assert(fw._ColdStoreLength == (fr.HasRealColdStore ? 0 : -1));
			fw._HotStoreLength = 0;
			goto Done;
		}

	NoFieldChanges:
	NoCoreFieldChanges:
		fw._ColdStoreLength = fw._HotStoreLength = -1;
		return;

	RewriteSchema:
		fw._FloatingFields?.Clear();
		fw.DeInitEntries();
		RewriteSchema(_SchemaId, ref fr, ref fw, hotStoreLimit);
	}

	[DoesNotReturn]
	private static void E_IndexBeyondLocalFieldCount_InvDat(long schemaId, int i, int xlc) {
		Debug.Assert(xlc >= 0);
		Debug.Assert((uint)i >= (uint)xlc);

		throw new InvalidDataException(
			$"Schema (with rowid {schemaId}) gave an invalid local field " +
			$"index {i}, which is " + (i < 0 ? "negative." : "not under " +
			$"{xlc}, the expected maximum number of local fields defined " +
			$"by the schema."));
	}
}
