namespace Kokoro.Internal;
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

		internal void InitEntries(int count) {
			_Entries = ArrayPool<FieldVal?>.Shared.Rent(count);
			_Offsets = ArrayPool<int>.Shared.Rent(count);
			// ^- NOTE: Must be done last. See code for `Dispose()`
		}

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

		// The check below ensures `0 <= xhc <= xlc`
		// - Necessary for when we finally allocate the "entries" buffer later
		// below, which would be `xlc` in length, to be cut by `xhc`.
		if (xlc < 0 || (uint)xhc > (uint)xlc) {
			E_InvalidLocalFieldCounts(_SchemaId, xhc: xhc, xlc: xlc);
		}

		[DoesNotReturn]
		static void E_InvalidLocalFieldCounts(long schemaId, int xhc, int xlc) {
			throw new InvalidDataException(
				$"Schema (with rowid {schemaId}) has {xhc} as its maximum " +
				$"hot field count while having {xlc} as its maximum local " +
				$"field count.");
		}

		// -=-

		fw.InitEntries(xlc);

		int fmi = int.MaxValue;
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

						Debug.Assert(fspec2.Index >= 0);
						fspec2.StoreType.DAssert_Defined();

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

						Debug.Assert(fspec.Index >= 0);
						fspec.StoreType.DAssert_Defined();

						if (fspec.StoreType != FieldStoreType.Shared) {
							int i = fspec.Index;

							if (i < fmi) fmi = i;
							if (i > lmi) lmi = i;

							if ((uint)i < (uint)xlc) {
								Debug.Assert((uint)xlc <= (uint)fw._Entries.Length);
								fw._Entries.DangerousGetReferenceAt(i) = fval;
							} else {
								E_IndexBeyondLocalFieldCount(_SchemaId, i: i, xlc: xlc);
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

		[DoesNotReturn]
		static void E_IndexBeyondLocalFieldCount(long schemaId, int i, int xlc) {
			Debug.Assert((uint)i >= (uint)xlc);
			throw new InvalidDataException(
				$"Schema (with rowid {schemaId}) gave a local field index " +
				$"(which is {i}) not under the expected maximum number of " +
				$"local fields it defined (which is {xlc}).");
		}

		// -=-

		// TODO Implement
		;

	NoFieldChanges:
		fw._ColdStoreLength = fw._HotStoreLength = -1;
		return;

	RewriteSchema:
		fw._FloatingFields?.Clear();
		fw.DeInitEntries();
		RewriteSchema(_SchemaId, ref fr, ref fw, hotStoreLimit);
	}

	[DoesNotReturn]
	private void E_TooManyFields(int count) {
		Debug.Assert(count > MaxFieldCount);
		throw new InvalidOperationException(
			$"Total number of fields (currently {count}) shouldn't exceed {MaxFieldCount};" +
			$"{Environment.NewLine}Entity: {GetDebugLabel()};" +
			$"{Environment.NewLine}Schema: {_SchemaId};");
	}
}
