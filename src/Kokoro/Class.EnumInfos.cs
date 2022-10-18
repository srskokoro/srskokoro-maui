namespace Kokoro;
using Blake2Fast;
using Blake2Fast.Implementation;
using Kokoro.Common.Util;
using Kokoro.Internal.Sqlite;
using Microsoft.Data.Sqlite;
using System.Runtime.InteropServices;

partial class Class {

	private EnumGroups? _EnumGroups;

	private sealed class EnumGroups : Dictionary<StringKey, List<EnumInfo>?> {
		public EnumGroupChanges? Changes;
	}

	private sealed class EnumGroupChanges : Dictionary<StringKey, List<EnumInfo>?> { }


	public readonly struct EnumInfo {
		private readonly FieldVal _Value;
		private readonly int _Ordinal;

		public readonly FieldVal Value => _Value;
		public readonly int Ordinal => _Ordinal;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public EnumInfo(FieldVal value, int ordinal) {
			_Value = value;
			_Ordinal = ordinal;
		}
	}


	public ICollection<StringKey> EnumGroupNames {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => _EnumGroups?.Keys ?? EmptyEnumGroupNames.Instance;
	}

	private static class EmptyEnumGroupNames {
		internal static readonly Dictionary<StringKey, List<EnumInfo>?>.KeyCollection Instance = new(new());
	}

	public void EnsureCachedEnumGroupName(StringKey name) {
		var enumGroups = _EnumGroups;
		if (enumGroups == null) {
			// This becomes a conditional jump forward to not favor it
			goto Init;
		}

	Set:
		enumGroups.TryAdd(name, null);
		return;

	Init:
		_EnumGroups = enumGroups = new();
		goto Set;
	}


	public bool TryGetEnumGroup(StringKey name, [MaybeNullWhen(false)] out List<EnumInfo> elems) {
		var enumGroups = _EnumGroups;
		if (enumGroups != null) {
			enumGroups.TryGetValue(name, out elems);
			return elems != null;
		}
		elems = null;
		return false;
	}

	public List<EnumInfo>? GetEnumGroup(StringKey name) {
		var enumGroups = _EnumGroups;
		if (enumGroups != null) {
			enumGroups.TryGetValue(name, out var elems);
			return elems;
		}
		return null;
	}

	public void SetEnumGroup(StringKey name, List<EnumInfo>? elems) {
		var enumGroups = _EnumGroups;
		if (enumGroups == null) {
			// This becomes a conditional jump forward to not favor it
			goto Init;
		}

		var changes = enumGroups.Changes;
		if (changes == null) {
			// This becomes a conditional jump forward to not favor it
			goto InitChanges;
		}

	Set:
		enumGroups[name] = elems;
		changes[name] = elems;
		return;

	Init:
		_EnumGroups = enumGroups = new();
	InitChanges:
		enumGroups.Changes = changes = new();
		goto Set;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void DeleteEnumGroup(StringKey name) => SetEnumGroup(name, null);

	/// <seealso cref="SetEnumGroupAsLoaded(StringKey, List{EnumInfo}?)"/>
	[SkipLocalsInit]
	public void SetCachedEnumGroup(StringKey name, List<EnumInfo>? elems) {
		var enumGroups = _EnumGroups;
		if (enumGroups == null) {
			// This becomes a conditional jump forward to not favor it
			goto Init;
		}

	Set:
		enumGroups[name] = elems;

		{
			var changes = enumGroups.Changes;
			// Optimized for the common case
			if (changes == null) {
				return;
			} else {
				ref var elemsRef = ref CollectionsMarshal.GetValueRefOrNullRef(changes, name);
				if (!U.IsNullRef(ref elemsRef)) {
					elemsRef = elems;
				}
				return;
			}
		}

	Init:
		_EnumGroups = enumGroups = new();
		goto Set;
	}

	/// <summary>
	/// Same as <see cref="UnmarkEnumGroupAsChanged(StringKey)"/> followed by
	/// <see cref="SetCachedEnumGroup(StringKey, List{EnumInfo}?)"/>.
	/// </summary>
	public void SetEnumGroupAsLoaded(StringKey name, List<EnumInfo>? elems) {
		var enumGroups = _EnumGroups;
		if (enumGroups == null) {
			// This becomes a conditional jump forward to not favor it
			goto Init;
		}

		enumGroups.Changes?.Remove(name);

	Set:
		enumGroups[name] = elems;
		return;

	Init:
		_EnumGroups = enumGroups = new();
		goto Set;
	}


	public void UnmarkEnumGroupAsChanged(StringKey name)
		=> _EnumGroups?.Changes?.Remove(name);

	public void UnmarkEnumGroupsAsChanged()
		=> _EnumGroups?.Changes?.Clear();


	public void UnloadEnumGroup(StringKey name) {
		var enumGroups = _EnumGroups;
		if (enumGroups != null) {
			enumGroups.Changes?.Remove(name);
			enumGroups.Remove(name);
		}
	}

	public void UnloadEnumGroups() {
		var enumGroups = _EnumGroups;
		if (enumGroups != null) {
			enumGroups.Changes = null;
			enumGroups.Clear();
		}
	}

	// --

	/// <remarks>
	/// CONTRACT:
	/// <br/>- Must be called while inside a transaction (ideally, using <see cref="OptionalReadTransaction"/>
	/// or <see cref="NestingWriteTransaction"/>).
	/// <br/>- Must call <see cref="KokoroSqliteDb.ReloadNameIdCaches()"/>
	/// beforehand, at least once, while inside the transaction.
	/// <para>
	/// Violation of the above contract may result in undefined behavior.
	/// </para>
	/// </remarks>
	[SkipLocalsInit]
	private void InternalLoadEnumGroup(KokoroSqliteDb db, StringKey name) {
		long enmGrp = db.LoadStaleNameId(name);
		if (enmGrp == 0) goto NotFound;

		// Load enum group
		using (var cmd = db.CreateCommand()) {
			cmd.Set(
				$"SELECT type,data,ord\n" +
				$"FROM {Prot.ClassToEnum}\n" +
				$"WHERE cls=$cls AND enmGrp=$enmGrp"
			).AddParams(
				new("$cls", _RowId),
				new("$enmGrp", enmGrp)
			);

			using var r = cmd.ExecuteReader();
			if (r.Read()) {
				List<EnumInfo> elems = new();

				// Pending changes will be discarded
				SetEnumGroupAsLoaded(name, elems);

				do {
					r.DAssert_Name(0, "type");
					FieldTypeHint typeHint = (FieldTypeHint)r.GetInt64(0);

					FieldVal fval;
					if (typeHint != FieldTypeHint.Null) {
						r.DAssert_Name(1, "data");
						var data = r.GetBytes(1);

						fval = new(typeHint, data);
					} else {
						fval = FieldVal.Null;
					}

					r.DAssert_Name(2, "ord");
					int ordinal = r.GetInt32(2);

					EnumInfo info = new(fval, ordinal);
					elems.Add(info);
				} while (r.Read());

				return; // Early exit
			}
		}

	NotFound:
		// Otherwise, either deleted or never existed.
		// Let that state materialize here then.
		UnloadEnumGroup(name);
	}

	/// <remarks>
	/// CONTRACT:
	/// <br/>- Must be called while inside a transaction (ideally, using <see cref="OptionalReadTransaction"/>
	/// or <see cref="NestingWriteTransaction"/>).
	/// <para>
	/// Violation of the above contract may result in undefined behavior.
	/// </para>
	/// </remarks>
	[SkipLocalsInit]
	private void InternalLoadEnumGroups(KokoroSqliteDb db) {
		db.ReloadNameIdCaches(); // Needed by `db.LoadStale…()` below

		using var cmd = db.CreateCommand();
		cmd.Set(
			$"SELECT enmGrp,type,data,ord\n" +
			$"FROM {Prot.ClassToEnum}\n" +
			$"WHERE cls=$cls\n" +
			$"ORDER BY enmGrp"
		).AddParams(new("$cls", _RowId));

		using var r = cmd.ExecuteReader();

		var enumGroups = _EnumGroups;
		if (enumGroups == null) {
			_EnumGroups = enumGroups = new();
		} else {
			// Pending changes will be discarded
			enumGroups.Changes = null;
			enumGroups.Clear();
		}

		long prevEnmGrp = 0;
		List<EnumInfo> elems = null!;

		while (r.Read()) {
			r.DAssert_Name(0, "enmGrp");
			long enmGrp = r.GetInt64(0);
			if (enmGrp != prevEnmGrp) {
				prevEnmGrp = enmGrp;

				var name = db.LoadStaleName(enmGrp);
				Debug.Assert(name is not null, "An FK constraint should've " +
					"been enforced to ensure this doesn't happen.");

				elems = new();
				enumGroups[name] = elems;
			}

			r.DAssert_Name(1, "type");
			FieldTypeHint typeHint = (FieldTypeHint)r.GetInt64(1);

			FieldVal fval;
			if (typeHint != FieldTypeHint.Null) {
				r.DAssert_Name(2, "data");
				var data = r.GetBytes(2);

				fval = new(typeHint, data);
			} else {
				fval = FieldVal.Null;
			}

			r.DAssert_Name(3, "ord");
			int ordinal = r.GetInt32(3);

			EnumInfo info = new(fval, ordinal);
			elems.Add(info);
		}
	}

	/// <remarks>
	/// CONTRACT:
	/// <br/>- Must be called while inside a transaction (ideally, using <see cref="OptionalReadTransaction"/>
	/// or <see cref="NestingWriteTransaction"/>).
	/// <para>
	/// Violation of the above contract may result in undefined behavior.
	/// </para>
	/// </remarks>
	[SkipLocalsInit]
	private void InternalLoadEnumGroupNames(KokoroSqliteDb db) {
		db.ReloadNameIdCaches(); // Needed by `db.LoadStale…()` below

		using var cmd = db.CreateCommand();
		cmd.Set($"SELECT enmGrp FROM {Prot.ClassToEnum} WHERE cls=$cls")
			.AddParams(new("$cls", _RowId));

		using var r = cmd.ExecuteReader();
		while (r.Read()) {
			long enmGrp = r.GetInt64(0);
			var name = db.LoadStaleName(enmGrp);
			Debug.Assert(name is not null, "An FK constraint should've been " +
				"enforced to ensure this doesn't happen.");
			EnsureCachedEnumGroupName(name);
		}
	}

	// --

	public void LoadEnumGroup(StringKey enumGroup1) {
		var db = Host.Db;
		using (new OptionalReadTransaction(db)) {
			if (Exists) {
				db.ReloadNameIdCaches();
				InternalLoadEnumGroup(db, enumGroup1);
			}
		}
	}

	public void LoadEnumGroup(StringKey enumGroup1, StringKey enumGroup2) {
		var db = Host.Db;
		using (new OptionalReadTransaction(db)) {
			if (Exists) {
				db.ReloadNameIdCaches();
				InternalLoadEnumGroup(db, enumGroup1);
				InternalLoadEnumGroup(db, enumGroup2);
			}
		}
	}

	public void LoadEnumGroup(StringKey enumGroup1, StringKey enumGroup2, StringKey enumGroup3) {
		var db = Host.Db;
		using (new OptionalReadTransaction(db)) {
			if (Exists) {
				db.ReloadNameIdCaches();
				InternalLoadEnumGroup(db, enumGroup1);
				InternalLoadEnumGroup(db, enumGroup2);
				InternalLoadEnumGroup(db, enumGroup3);
			}
		}
	}

	public void LoadEnumGroup(StringKey enumGroup1, StringKey enumGroup2, StringKey enumGroup3, StringKey enumGroup4) {
		var db = Host.Db;
		using (new OptionalReadTransaction(db)) {
			if (Exists) {
				db.ReloadNameIdCaches();
				InternalLoadEnumGroup(db, enumGroup1);
				InternalLoadEnumGroup(db, enumGroup2);
				InternalLoadEnumGroup(db, enumGroup3);
				InternalLoadEnumGroup(db, enumGroup4);
			}
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void LoadEnumGroup(params StringKey[] enumGroups)
		=> LoadEnumGroup(enumGroups.AsSpan());

	public void LoadEnumGroup(ReadOnlySpan<StringKey> enumGroups) {
		var db = Host.Db;
		using (new OptionalReadTransaction(db)) {
			if (Exists) {
				db.ReloadNameIdCaches();
				foreach (var enumGroup in enumGroups)
					InternalLoadEnumGroup(db, enumGroup);
			}
		}
	}

	public void LoadEnumGroups() {
		var db = Host.Db;
		using (new OptionalReadTransaction(db)) {
			if (Exists)
				InternalLoadEnumGroups(db);
		}
	}

	public void LoadEnumGroupNames() {
		var db = Host.Db;
		using (new OptionalReadTransaction(db)) {
			if (Exists)
				InternalLoadEnumGroupNames(db);
		}
	}

	// --

	/// <remarks>
	/// CONTRACT:
	/// <br/>- Must be called while inside a transaction (ideally, using <see cref="NestingWriteTransaction"/>).
	/// <para>
	/// Violation of the above contract may result in undefined behavior.
	/// </para>
	/// </remarks>
	[SkipLocalsInit]
	private static void InternalSaveEnumGroups(KokoroSqliteDb db, Dictionary<StringKey, List<EnumInfo>?> changes, long clsId) {
		var changes_iter = changes.GetEnumerator();
		if (!changes_iter.MoveNext()) goto NoChanges;

		db.ReloadNameIdCaches(); // Needed by `db.LoadStale…()` below

		SqliteCommand?
			updCmd = null,
			delCmd = null;

		SqliteParameter
			cmd_cls = new("$cls", clsId),
			cmd_enmGrp = new() { ParameterName = "$enmGrp" };

		SqliteParameter
			updCmd_ord = null!,
			updCmd_type = null!,
			updCmd_data = null!,
			updCmd_csum = null!;

		try {
		Loop:
			var (enumGroup, elems) = changes_iter.Current;
			long enumGroupId;
			if (elems != null) {
				if (elems.Count != 0) {
					goto HasElems;
				} else {
					goto EmptyElems;
				}
			} else {
				goto NoElems;
			}

		EmptyElems:
			elems = null;
		NoElems:
			enumGroupId = db.LoadStaleNameId(enumGroup);
			if (enumGroupId != 0) {
				goto DoProcess;
			} else {
				// Deletion requested, but there's nothing to delete, as the
				// enum group name is nonexistent.
				goto Continue;
			}

		HasElems:
			enumGroupId = db.LoadStaleOrEnsureNameId(enumGroup);
		DoProcess:
			if (delCmd != null) {
				goto DeleteEnumGroup;
			} else {
				goto InitToDeleteEnumGroup;
			}

		DeleteEnumGroup:
			{
				cmd_enmGrp.Value = enumGroupId;

				// NOTE: It's possible for nothing to be deleted, for when the
				// enum group didn't exist in the first place.
				delCmd.ExecuteNonQuery();

				if (elems != null) {
					if (updCmd != null) {
						goto UpdateEnumGroup;
					} else {
						goto InitToUpdateEnumGroup;
					}
				} else {
					// Nothing to update.
					goto Continue;
				}
			}

		UpdateEnumGroup:
			{
				// Used to generate each field enum element's `csum`
				var hasher_base = Blake2b.CreateIncrementalHasher(EnumInfoCsumDigestLength);
				/// WARNING: The expected order of inputs to be fed to the above
				/// hasher must be strictly as follows:
				///
				/// 0. `enumGroup` name string in UTF8 with its (32-bit) length
				/// prepended.
				/// 1. `elem.Ordinal`
				/// 2. `elem.Value`'s <see cref="FieldVal.FeedTo(ref Blake2bHashState)"/>
				/// method, provided that, the aforesaid method treats the field
				/// value as several inputs composed of the following, in strict
				/// order:
				///   2.1. The type hint, as a 32-bit integer.
				///   2.2. If not a null field value (indicated by the type
				///   hint), then the field value data bytes with its (32-bit)
				///   length prepended.
				///     - NOTE: The length said here is the length of the field
				///     value data bytes, excluding the type hint.
				///
				/// Unless stated otherwise, all integer inputs should be
				/// consumed in their little-endian form. <see href="https://en.wikipedia.org/wiki/Endianness"/>
				///
				/// The resulting hash BLOB shall be prepended with a version
				/// varint. Should any of the following happens, the version
				/// varint must change:
				///
				/// - The resulting hash BLOB length changes.
				/// - The algorithm for the resulting hash BLOB changes.
				/// - An input entry (from the list of inputs above) was
				/// removed.
				/// - The order of an input entry (from the list of inputs
				/// above) was changed or shifted.
				/// - An input entry's size (in bytes) changed while it's
				/// expected to be fixed-sized (e.g., not length-prepended).
				///
				/// The version varint needs not to change if further input
				/// entries were to be appended (from the list of inputs above),
				/// provided that the last input entry has a clear termination,
				/// i.e., fixed-sized or length-prepended.

				Debug.Assert(cmd_enmGrp.Value is long v && v == enumGroupId);
				hasher_base.UpdateWithLELength(enumGroup.Value.ToUTF8Bytes());

				foreach (var elem in elems) {
					Types.DAssert_IsValueType(hasher_base);
					var hasher = hasher_base; // Value copy semantics

					// Used only to help assert the hashing contract
					int hasher_debug_i = 1;

					updCmd_ord.Value = elem.Ordinal;
					hasher.UpdateLE(elem.Ordinal);
					Debug.Assert(1 == hasher_debug_i++);

					var fval = elem.Value;
					updCmd_type.Value = (long)(FieldTypeHintInt)fval.TypeHint;
					updCmd_data.Value = fval.DangerousGetDataBytes();
					fval.FeedTo(ref hasher);
					Debug.Assert(2 == hasher_debug_i++);

					byte[] csum = FinishWithEnumInfoCsum(ref hasher);
					updCmd_csum.Value = csum;

					try {
						int updated = updCmd.ExecuteNonQuery();
						Debug.Assert(updated == 1, $"Updated: {updated}");
					} catch (SqliteException ex) when (
						ex.SqliteErrorCode == SQLitePCL.raw.SQLITE_TOOBIG
					) {
						E_EnumFValDataTooLarge(db, (uint)fval.Data.Length);
						return;
					}

					// Allow early GC
					updCmd_data.Value = null;
				}

				goto Continue;
			}

		Continue:
			if (!changes_iter.MoveNext()) {
				goto Break;
			} else {
				// This becomes a conditional jump backward -- similar to a
				// `do…while` loop.
				goto Loop;
			}

		InitToDeleteEnumGroup:
			{
				delCmd = db.CreateCommand();
				delCmd.Set(
					$"DELETE FROM {Prot.ClassToEnum} WHERE (cls,enmGrp)=($cls,$enmGrp)"
				).AddParams(
					cmd_cls, cmd_enmGrp
				);
				Debug.Assert(
					cmd_cls.Value != null
				);
				goto DeleteEnumGroup;
			}

		InitToUpdateEnumGroup:
			{
				updCmd = db.CreateCommand();
				updCmd.Set(
					$"INSERT INTO {Prot.ClassToEnum}(cls,enmGrp,csum,ord,type,data)\n" +
					$"VALUES($cls,$enmGrp,$csum,$ord,$type,$data)"
				).AddParams(
					cmd_cls, cmd_enmGrp,
					updCmd_ord = new() { ParameterName = "$ord" },
					updCmd_type = new() { ParameterName = "$type" },
					updCmd_data = new() { ParameterName = "$data" },
					updCmd_csum = new() { ParameterName = "$csum" }
				);
				Debug.Assert(
					cmd_cls.Value != null
				);
				goto UpdateEnumGroup;
			}

		Break:
			;

		} finally {
			updCmd?.Dispose();
			delCmd?.Dispose();
		}

	NoChanges:
		;
	}

	private const int EnumInfoCsumDigestLength = 31; // 248-bit hash

	private static byte[] FinishWithEnumInfoCsum(ref Blake2bHashState hasher) {
		const int CsumVer = 1; // The version varint
		const int CsumVerLength = 1; // The varint length is a single byte for now
		VarInts.DAssert_Equals(stackalloc byte[CsumVerLength] { CsumVer }, CsumVer);

		Span<byte> csum = stackalloc byte[CsumVerLength + EnumInfoCsumDigestLength];
		hasher.Finish(csum.Slice(CsumVerLength));

		// Prepend version varint
		csum[0] = CsumVer;

		// TODO In the future, once we're either using `sqlite3_stmt` directly or have replaced `Microsoft.Data.Sqlite`
		// with a custom version more suited to our needs, rent/stackalloc a buffer for the hash output instead, then
		// pass that as a `ReadOnlySpan<byte>` to `sqlite3_bind_blob()`.
		return csum.ToArray();
	}

	[DoesNotReturn]
	private static void E_EnumFValDataTooLarge(KokoroSqliteDb db, uint currentSize) {
		long limit = SQLitePCL.raw.sqlite3_limit(db.Handle, SQLitePCL.raw.SQLITE_LIMIT_LENGTH, -1);
		throw new InvalidOperationException(
			$"Total number of bytes for field enum's field value data " +
			$"(currently {currentSize}) caused the DB row to exceed the limit" +
			$" of {limit} bytes.");
	}
}
