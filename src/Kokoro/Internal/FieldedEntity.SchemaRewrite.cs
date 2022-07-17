namespace Kokoro.Internal;
using Kokoro.Common.Buffers;
using Kokoro.Internal.Sqlite;
using Microsoft.Data.Sqlite;
using System.Runtime.InteropServices;

partial class FieldedEntity {
	private const int MaxClassCount = byte.MaxValue + 1;

	private HashSet<long>? _AddedClasses;
	private HashSet<long>? _RemovedClasses;

	// --

	[SuppressMessage("Style", "IDE1006:Naming Styles")]
	private static class SchemaRewrite {

		internal struct ClassInfo {
			public long rowid;
			public UniqueId uid;
			public byte[] csum;
			public int ord;

			public ClassInfo(
				long rowid, UniqueId uid, byte[] csum, int ord
			) {
				this.rowid = rowid;
				this.uid = uid;
				this.csum = csum;
				this.ord = ord;
			}
		}

		internal struct FieldInfo {
			public long rowid;

			public int cls_ord;
			public FieldStoreType sto;
			public int ord;
			public FieldSpec src_idx_a_sto;

			public long atarg;
			public string name;

			public FieldVal? new_fval;
			public UniqueId cls_uid;

			// TODO Consider putting this in a union
			public int atarg_x;

			public FieldInfo(
				long rowid,
				int cls_ord, FieldStoreType sto, int ord, FieldSpec src_idx_a_sto,
				long atarg, string name, FieldVal? new_fval, UniqueId cls_uid
			) {
				U.SkipInit(out this);
				this.rowid = rowid;

				this.cls_ord = cls_ord;
				this.sto = sto;
				this.ord = ord;
				this.src_idx_a_sto = src_idx_a_sto;

				this.atarg = atarg;
				this.name = name;
				this.new_fval = new_fval;
				this.cls_uid = cls_uid;
			}

			[StructLayout(LayoutKind.Explicit)]
			internal struct Union {
				// TODO-XXX Eventually use to reduce outer `struct` size
			}
		}
	}

	/// <remarks>
	/// CONTRACT: Must be called while inside a transaction (ideally, using <see cref="NestingWriteTransaction"/>).
	/// <para>
	/// Violation of the above contract may result in undefined behavior.
	/// </para>
	/// </remarks>
	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	[SkipLocalsInit]
	private protected void RewriteSchema(ref FieldsReader fr, int hotStoreLimit, ref FieldsWriter fw) {
		DAssert_FieldsWriterPriorRewrite(ref fw);

		var clsSet = _AddedClasses;
		clsSet = clsSet != null ? new(clsSet) : new();

		var remClsSet = _RemovedClasses;
		remClsSet = remClsSet != null ? new(remClsSet) : clsSet;

		// Get the old schema's direct classes
		var db = fr.Db;
		using (var cmd = db.CreateCommand()) {
			cmd.Set("SELECT cls FROM SchemaToDirectClass WHERE schema=$schema")
				.AddParams(new("$schema", _SchemaRowId));

			using var r = cmd.ExecuteReader();
			while (r.Read()) {
				r.DAssert_Name(0, "cls");
				long cls = r.GetInt64(0);

				if (!remClsSet.Contains(cls))
					clsSet.Add(cls);
			}
		}

		int dclsCount = clsSet.Count;
		if (dclsCount > MaxClassCount) goto E_TooManyClasses;

		List<SchemaRewrite.ClassInfo> clsList = new(dclsCount);

		// Get the needed info for each class while also gathering all the
		// indirect classes
		using (var clsCmd = db.CreateCommand()) {
			SqliteParameter clsCmd_rowid;
			clsCmd.Set("SELECT uid,csum,ord FROM Class WHERE rowid=$rowid")
				.AddParams(clsCmd_rowid = new() { ParameterName = "$rowid" });

			// Get the needed info for each direct class
			foreach (var cls in clsSet) {
				clsCmd_rowid.Value = cls;

				using var r = clsCmd.ExecuteReader();
				if (r.Read()) {
					r.DAssert_Name(0, "uid");
					UniqueId uid = r.GetUniqueId(0);

					r.DAssert_Name(1, "csum");
					byte[] csum = r.GetBytes(1);

					r.DAssert_Name(2, "ord");
					int ord = r.GetInt32(2);

					clsList.Add(new(rowid: cls, uid, csum, ord));
				} else {
					// User attached a nonexistent class to the fielded entity.
					// Ignore it then.
				}
			}

			SqliteParameter inclCmd_cls;
			using var inclCmd = db.CreateCommand();
			inclCmd.Set("SELECT incl FROM ClassToInclude WHERE cls=$cls")
				.AddParams(inclCmd_cls = new() { ParameterName = "$cls" });

			// Recursively get all included classes, skipping already seen
			// classes to prevent infinite recursion
			for (int i = 0; i < clsList.Count; i++) {
				ref var cls = ref clsList.AsSpan().DangerousGetReferenceAt(i);
				inclCmd_cls.Value = cls.rowid;

				using var inclCmd_r = inclCmd.ExecuteReader();
				while (inclCmd_r.Read()) {
					inclCmd_r.DAssert_Name(0, "incl");
					long incl = inclCmd_r.GetInt64(0);

					if (clsSet.Add(incl)) {
						// Get the needed info for the indirect class
						// --
						clsCmd_rowid.Value = incl;

						using var r = clsCmd.ExecuteReader();
						if (r.Read()) {
							r.DAssert_Name(0, "uid");
							UniqueId uid = r.GetUniqueId(0);

							r.DAssert_Name(1, "csum");
							byte[] csum = r.GetBytes(1);

							r.DAssert_Name(2, "ord");
							int ord = r.GetInt32(2);

							clsList.Add(new(rowid: incl, uid, csum, ord));
						} else {
							Debug.Fail("An FK constraint should've been " +
								"enforced to ensure this doesn't happen.");
						}
					}
				}
			}
		}

		if (clsSet.Count > MaxClassCount) goto E_TooManyClasses;

		// Gather old field mappings from the old schema, by mapping a field id
		// to a pair of field change value and old field spec.
		// --

		Dictionary<long, (FieldVal? FVal, FieldSpec FSpec)> fldMapOld;
		{
			// Process the field changes
			// --

			Dictionary<StringKey, FieldVal>? fchanges = _FieldChanges;
			if (fchanges == null) goto NoFieldChanges;

			var fchanges_iter = fchanges.GetEnumerator();
			if (!fchanges_iter.MoveNext()) goto NoFieldChanges;

			fldMapOld = new(fchanges.Count);

			db.ReloadFieldNameCaches(); // Needed by `db.LoadStale…()` below
			do {
				var (fname, fval) = fchanges_iter.Current;
				long fld = db.LoadStaleOrEnsureFieldId(fname);

				bool added = fldMapOld.TryAdd(fld, (fval, -1));
				Debug.Assert(added, $"Shouldn't happen if running in a " +
					$"`{nameof(NestingWriteTransaction)}` or equivalent");
			} while (fchanges_iter.MoveNext());

			Debug.Assert(fldMapOld.Count == fchanges.Count);

			// --
			goto DoneWithFieldChanges;

		NoFieldChanges:
			fldMapOld = new();

		DoneWithFieldChanges:
			;

			// Retrieve the field specs from the old schema
			// --

			using (var cmd = db.CreateCommand()) {
				cmd.Set("SELECT fld,idx_a_sto FROM SchemaToField WHERE schema=$schema")
					.AddParams(new("$schema", _SchemaRowId));

				using var r = cmd.ExecuteReader();
				while (r.Read()) {
					r.DAssert_Name(0, "fld");
					long fld = r.GetInt64(0);

					r.DAssert_Name(1, "idx_a_sto");
					FieldSpec fspec = r.GetInt32(1);
					Debug.Assert(fspec >= (int)0);

					ref var entry = ref CollectionsMarshal.GetValueRefOrAddDefault(
						fldMapOld, fld, out bool exists
					);
					entry.FSpec = fspec;

					Debug.Assert(exists || entry.FVal == null);
				}
			}
		}

		// Gather the fields associated with each class, skipping duplicates,
		// while also fetching any needed info
		// --

		List<SchemaRewrite.FieldInfo> fldList = new();

		int fldSharedCount = 0;
		int fldHotCount = 0;
		int fldColdCount = 0;

		const FieldStoreType FieldStoreType_Alias_Resolved = unchecked((FieldStoreType)(-3));
		const FieldStoreType FieldStoreType_Alias_Resolving = unchecked((FieldStoreType)(-2));
		const FieldStoreType FieldStoreType_Alias_Unresolved = unchecked((FieldStoreType)(-1));

		// Used to spot duplicate entries and to resolve field alias targets
		Dictionary<long, int> fldMap = new();

		using (var cmd = db.CreateCommand()) {
			SqliteParameter cmd_cls;
			cmd.Set(
				"SELECT\n" +
					"fld.rowid AS fld,\n" +
					"fld.name AS name,\n" +
					"cls2fld.ord AS ord,\n" +
					"ifnull(cls2fld.sto,-1) AS sto,\n" +
					"ifnull(cls2fld.atarg,0) AS atarg\n" +
				"FROM ClassToField AS cls2fld,FieldName AS fld\n" +
				"WHERE cls2fld.cls=$cls AND fld.rowid=cls2fld.fld"
			).AddParams(
				cmd_cls = new() { ParameterName = "$cls" }
			);

			foreach (ref var cls in clsList.AsSpan()) {
				cmd_cls.Value = cls.rowid;

				using var r = cmd.ExecuteReader();
				while (r.Read()) {
					r.DAssert_Name(0, "fld");
					long fld = r.GetInt64(0);

					r.DAssert_Name(1, "name");
					string name = r.GetString(1);

					r.DAssert_Name(2, "ord");
					int ord = r.GetInt32(2);

					r.DAssert_Name(3, "sto");
					FieldStoreType sto = (FieldStoreType)r.GetInt32(3);

					long atarg;
					if ((FieldStoreTypeSInt)sto >= 0) {
						sto.DAssert_Defined();
						atarg = 0;
					} else {
						Debug.Assert(sto == FieldStoreType_Alias_Unresolved);

						// Avoid bogus data on release builds as this will be
						// used to maneuver unsafe constructs.
						sto = FieldStoreType_Alias_Unresolved;

						r.DAssert_Name(4, "atarg");
						atarg = r.GetInt64(4);
					}

					ref int i = ref CollectionsMarshal.GetValueRefOrAddDefault(fldMap, fld, out bool exists);
					if (!exists) {
						i = fldList.Count; // The new entry's index in the list

						FieldVal? new_fval;
						FieldSpec src_idx_a_sto;

						if (fldMapOld.Remove(fld, out var oldMapping)) {
							(new_fval, src_idx_a_sto) = oldMapping;
							Debug.Assert(src_idx_a_sto >= (int)0 || new_fval != null);
						} else {
							// Case: Field not defined by the old schema
							new_fval = OnSupplantFloatingField(db, fld) ?? FieldVal.Null;
							// Indicate that it was a floating field
							src_idx_a_sto = -2;
						}

						// Add the new entry
						fldList.Add(new(
							rowid: fld,
							cls_ord: cls.ord, sto, ord, src_idx_a_sto,
							atarg, name, new_fval, cls_uid: cls.uid
						));
					} else {
						// Get a `ref` to the already existing entry
						ref var entry = ref fldList.AsSpan().DangerousGetReferenceAt(i);

						Debug.Assert(name == entry.name, $"Impossible! " +
							$"Two fields have the same rowid (which is {fld}) but different names:{Environment.NewLine}" +
							$"Name of 1st instance: {name}{Environment.NewLine}" +
							$"Name of 2nd instance: {entry.name}");

						// Attempt to replace existing entry
						// --
						{
							var a = cls.ord; var b = entry.cls_ord;
							if (a != b) {
								if (a < b) goto ReplaceEntry;
								else goto LeaveEntry;
							}
						}
						{
							// Cast to signed, to give field alias entries
							// higher priority when it comes to replacing
							// existing entries.
							var a = (FieldStoreTypeSInt)sto;
							var b = (FieldStoreTypeSInt)entry.sto;
							if (a != b) {
								if (a < b) goto ReplaceEntry;
								else goto LeaveEntry;
							}
						}
						{
							var a = ord; var b = entry.ord;
							if (a != b) {
								if (a < b) goto ReplaceEntry;
								else goto LeaveEntry;
							}
						}
						// `atarg` may be different at this point: the class
						// with the lowest UID gets to define `atarg`
						{
							var a = cls.uid; var b = entry.cls_uid;
							if (a != b) {
								if (a < b) goto ReplaceEntry;
								else goto LeaveEntry;
							}
						}
						{
							// Same field. Same class. It's the same entry.
							Debug.Fail($"Unexpected! Duplicate rows returned for class-and-field pair:{Environment.NewLine}" +
								$"Class UID: {cls.uid}{Environment.NewLine}" +
								$"Field Name: {name}{Environment.NewLine}" +
								$"Field ROWID: {fld}");

							// It's the same entry anyway.
							goto LeaveEntry;
						}

					ReplaceEntry:
						{
							var entry_sto = entry.sto;
							if (entry_sto != FieldStoreType.Shared) {
								if (entry_sto == FieldStoreType.Hot) {
									fldHotCount--;
								} else if ((FieldStoreTypeSInt)entry_sto >= 0) {
									fldColdCount--;
								}
							} else {
								fldSharedCount--;
							}
						}
						entry = new(
							rowid: fld,
							cls_ord: cls.ord, sto, ord, entry.src_idx_a_sto,
							atarg, name, entry.new_fval, cls_uid: cls.uid
						);
					}

					Debug.Assert(typeof(FieldStoreTypeInt) == typeof(FieldStoreTypeUInt));

					if (sto != FieldStoreType.Shared) {
						if (sto == FieldStoreType.Hot) {
							fldHotCount++;
						} else if ((FieldStoreTypeSInt)sto >= 0) {
							fldColdCount++;
						}
					} else {
						fldSharedCount++;
					}

				LeaveEntry:
					;
				}
			}
		}

		if (fldList.Count > MaxFieldCount) goto E_TooManyFields;

		// The remaining old field mappings will become floating fields -- i.e.,
		// fields not defined by the (new) schema.
		{
			int fldMapOldCount = fldMapOld.Count;
			if (fldMapOldCount != 0) {
				var floFlds = fw._FloatingFields = new(fldMapOldCount);
				foreach (var (fld, entry) in fldMapOld) {
					floFlds.Add((fld, entry.FVal ?? fr.Read(entry.FSpec)));
				}
			}
		}

		// -=-
		{
			Debug.Assert(fldList.Count <= byte.MaxValue + 1);
			Debug.Assert(clsList.Count <= byte.MaxValue + 1);

			[MethodImpl(MethodImplOptions.AggressiveOptimization)]
			static void InitWithIndices(Span<byte> span) {
				ref var r0 = ref span.DangerousGetReference();
				for (int i = span.Length; --i >= 0;) {
					U.Add(ref r0, i) = (byte)i;
				}
				Debug.Assert(span.Length <= 0 || (
					U.Add(ref r0, 0) == 0 &&
					U.Add(ref r0, span.Length/2) == span.Length/2 &&
					U.Add(ref r0, span.Length-1) == span.Length-1
				));
			}

			using var renter_fldListIdxs = BufferRenter<byte>
				.CreateSpan(fldList.Count, out var fldListIdxs);
			InitWithIndices(fldListIdxs);

			using var renter_clsListIdxs = BufferRenter<byte>
				.CreateSpan(clsList.Count, out var clsListIdxs);
			InitWithIndices(clsListIdxs);

			// Sort the gathered lists by sorting their indices instead
			{
				// Sort the list of fields
				var fldList_capture = fldList;
				fldListIdxs.Sort(int (byte x, byte y) => {
					ref var r0 = ref fldList_capture.AsSpan().DangerousGetReference();
					ref var a = ref U.Add(ref r0, x);
					ref var b = ref U.Add(ref r0, y);

					int cmp;
					// Partition the sorted array by field store type
					{
						// NOTE: Using `Enum.CompareTo()` has a boxing cost,
						// which sadly, JIT doesn't optimize out (for now). So
						// we must cast the enums to their int counterparts to
						// avoid the unnecessary box.
						var a_sto = (FieldStoreTypeInt)a.sto;
						var b_sto = (FieldStoreTypeInt)b.sto;
						cmp = a_sto.CompareTo(b_sto);
						if (cmp != 0) goto Return;
					}
					{
						cmp = a.cls_ord.CompareTo(b.cls_ord);
						if (cmp != 0) goto Return;
					}
					{
						cmp = a.ord.CompareTo(b.ord);
						if (cmp != 0) goto Return;
					}
					{
						Debug.Assert(a.rowid != b.rowid, $"Expecting no " +
							$"duplicates but found a duplicate field entry " +
							$"with rowid {a.rowid}");
					}
					{
						cmp = string.CompareOrdinal(a.name, b.name);
						Debug.Assert(cmp != 0, $"Impossible! Two fields have " +
							$"different rowids ({a.rowid} and {b.rowid}) but " +
							$"same name: {a.name}");
					}
				Return:
					return cmp;
				});

				// Partition the list of classes into two, direct and indirect
				// classes, then sort each partition separately.
				// --

				var clsList_capture = clsList;

				int clsList_comparison(byte x, byte y) {
					ref var r0 = ref clsList_capture.AsSpan().DangerousGetReference();
					return U.Add(ref r0, x).uid.CompareTo(U.Add(ref r0, y).uid);
				}

				// Sort the list of direct classes
				clsListIdxs[..dclsCount].Sort(clsList_comparison);

				// Sort the list of indirect classes
				clsListIdxs[dclsCount..].Sort(clsList_comparison);
			}

			[Conditional("DEBUG")]
			static void DAssert_fldListIdxs_AssumedLayoutIsCorrect() {
				Span<FieldStoreType> expected = stackalloc FieldStoreType[] {
					FieldStoreType.Shared,
					FieldStoreType.Hot,
					FieldStoreType.Cold,
					FieldStoreType_Alias_Resolved,
					FieldStoreType_Alias_Resolving,
					FieldStoreType_Alias_Unresolved,
				};

				Span<FieldStoreType> actual = stackalloc FieldStoreType[expected.Length];
				expected.CopyTo(actual);

				actual.Sort();

				bool eq = expected.SequenceEqual(actual);
				Debug.Assert(eq, $"Arrangement of values in `{nameof(fldListIdxs)}` may not be as expected.");
			}

			DAssert_fldListIdxs_AssumedLayoutIsCorrect(); // Future-proofing

			// -=-

			int fldBaseCount = fldSharedCount + fldHotCount + fldColdCount;

			// Resolve field aliases and propagate field changes to their target
			{
				int k = fldBaseCount;
				int n = fldList.Count;

				Debug.Assert((uint)k <= (uint)n);
				if (k >= n) goto FieldAliasesResolved;

				Debug.Assert(fldList.Count == fldListIdxs.Length);
				ref var flds_r0 = ref fldList.AsSpan().DangerousGetReference();
				ref byte fldIdxs_r0 = ref fldListIdxs.DangerousGetReference();

				int fldAliasAsColdCount = 0;
				do {
					int init_i = U.Add(ref fldIdxs_r0, k);
					Debug.Assert((uint)init_i < (uint)n);
					ref var init_alias = ref U.Add(ref flds_r0, init_i);

					if (init_alias.sto == FieldStoreType_Alias_Resolved) {
						// Case: Field alias has already been resolved before
						continue;
					}

					ref var alias = ref init_alias;
					int i = init_i;

					alias.atarg_x = -1;

				ResolveFieldAlias:
					// --

					if (!fldMap.TryGetValue(alias.atarg, out int x)) {
						// Case: Target not found

						// This becomes a conditional jump forward to not favor it
						goto Fallback;
					}

					Debug.Assert((uint)x < (uint)n);
					ref var target = ref U.Add(ref flds_r0, x);

					if ((FieldStoreTypeSInt)target.sto < 0) {
						// Case: Target is also a field alias

						// This becomes a conditional jump forward to not favor it
						goto TargetNeedsResolution;
					}

				TargetResolved:
					// Case: Field alias target resolved
					;

					// Propagate any field change to the target
					if (alias.src_idx_a_sto != -2) {
						// Case: The field alias might have an actual field
						// change value (that didn't come from its old floating
						// field store).

					}

					// Backtrack as much as possible in the chain of references,
					// assigning the resolved index to all field alias entries
					// encountered and marking each as concluded.
					{
						U.SkipInit(out int p);
						goto SetResolution; // Skip below

					Backtrack:
						Debug.Assert(!U.AreSame(ref init_alias, ref alias));
						alias.sto = FieldStoreType_Alias_Resolved;
						alias = ref U.Add(ref flds_r0, p); // Go back in chain

					SetResolution:
						p = alias.atarg_x; // Get as the backtrack index
						alias.atarg_x = x; // Set as the resolved index

						if (p < 0) {
							Debug.Assert(U.AreSame(ref init_alias, ref alias));
							if ((FieldStoreTypeSInt)alias.sto < 0) {
								Debug.Assert(alias.sto == FieldStoreType_Alias_Resolving);
								alias.sto = FieldStoreType_Alias_Resolved;
							} else {
								Debug.Assert(alias.sto == FieldStoreType.Cold);
							}
							goto NextFieldAlias; // We're done!
						} else {
							// This becomes a conditional jump backward --
							// similar to a `do…while` loop.
							goto Backtrack;
						}

#pragma warning disable CS0162 // Unreachable code detected
						Debug.Fail("This point should be unreachable.");
#pragma warning restore CS0162
					}

				TargetNeedsResolution:
					// Mark in case of circular reference or self-reference
					alias.sto = FieldStoreType_Alias_Resolving;

					if (target.sto == FieldStoreType_Alias_Unresolved) {
						// Case: It's another unresolved field alias

						// Save to backtrack chain of references
						target.atarg_x = i;

						alias = ref target;
						i = x;

						goto ResolveFieldAlias;
					} else if (target.sto != FieldStoreType_Alias_Resolving) {
						// Case: It's an already resolved field alias
						Debug.Assert(target.sto == FieldStoreType_Alias_Resolved);

					} else {
						// Case: Resulted in a circular reference
						Debug.Assert(target.sto == FieldStoreType_Alias_Resolving); // Future-proofing

						goto Fallback;
					}

				Fallback:
					{
						// Convert the field alias that initiated all this into
						// a cold field, then make that the target of all field
						// alias entries in the chain of references.
						init_alias.sto = FieldStoreType.Cold;
						// ^- NOTE: The above won't conflict with the opening
						// check for already resolved field aliases, since we're
						// converting only the initial field alias, which we'll
						// skip eventually as we advance to the next field in
						// the list of field aliases.

						target = ref init_alias;
						x = init_i;

						fldAliasAsColdCount++;
						goto TargetResolved;
					}

				NextFieldAlias:
					;

				} while (++k < n);

				// If there were any field aliases that are now cold fields, we
				// must sort the list of indices again.
				if (fldAliasAsColdCount > 0) {
					fldBaseCount += fldAliasAsColdCount;
					fldColdCount += fldAliasAsColdCount;
					// TODO-XXX Implement sorting
				}

			FieldAliasesResolved:
				;
			}

		}

		// TODO-XXX Finish implementation

		return; // ---

	E_TooManyFields:
		E_TooManyFields(fldList.Count);

	E_TooManyClasses:
		E_TooManyClasses(clsSet.Count);
	}

	[DoesNotReturn]
	private void E_TooManyClasses(int currentCount) {
		Debug.Assert(currentCount > MaxClassCount);
		throw new InvalidOperationException(
			$"Total number of classes (currently {currentCount}) shouldn't exceed {MaxClassCount};" +
			$"{Environment.NewLine}Entity: {GetDebugLabel()};" +
			$"{Environment.NewLine}Schema: {_SchemaRowId};");
	}
}
