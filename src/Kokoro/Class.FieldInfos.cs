﻿namespace Kokoro;
using Blake2Fast;
using Blake2Fast.Implementation;
using Kokoro.Common.Util;
using Kokoro.Internal.Sqlite;
using Microsoft.Data.Sqlite;
using System.Runtime.InteropServices;

partial class Class {

	private FieldInfos? _FieldInfos;

	private sealed class FieldInfos : Dictionary<StringKey, FieldInfo> {
		public FieldInfoChanges? Changes;
	}

	private sealed class FieldInfoChanges : Dictionary<StringKey, FieldInfo> { }


	public readonly struct FieldInfo {
		internal readonly bool _IsLoaded;

		private readonly FieldStoreType _StoreType;
		private readonly int _Ordinal;

		private readonly StringKey? _EnumGroup;

		public readonly int Ordinal => _Ordinal;
		public readonly FieldStoreType StoreType => _StoreType;

		public readonly StringKey? EnumGroup => _EnumGroup;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FieldInfo(int ordinal, FieldStoreType storeType)
			: this(ordinal, null, storeType) { }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FieldInfo(int ordinal, StringKey? enumGroup, FieldStoreType storeType) {
			_IsLoaded = true;
			_Ordinal = ordinal;
			_StoreType = storeType;
			_EnumGroup = enumGroup;
		}
	}


	public ICollection<StringKey> FieldNames {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => _FieldInfos?.Keys ?? EmptyFieldNames.Instance;
	}

	private static class EmptyFieldNames {
		internal static readonly Dictionary<StringKey, FieldInfo>.KeyCollection Instance = new(new());
	}

	public void EnsureCachedFieldName(StringKey name) {
		var infos = _FieldInfos;
		if (infos == null) {
			// This becomes a conditional jump forward to not favor it
			goto Init;
		}

	Set:
		infos.TryAdd(name, default);
		return;

	Init:
		_FieldInfos = infos = new();
		goto Set;
	}


	public bool TryGetFieldInfo(StringKey name, [MaybeNullWhen(false)] out FieldInfo info) {
		var infos = _FieldInfos;
		if (infos != null) {
			infos.TryGetValue(name, out info);
			return info._IsLoaded;
		}
		info = default;
		return false;
	}

	public void SetFieldInfo(StringKey name, FieldInfo info) {
		var infos = _FieldInfos;
		if (infos == null) {
			// This becomes a conditional jump forward to not favor it
			goto Init;
		}

		var changes = infos.Changes;
		if (changes == null) {
			// This becomes a conditional jump forward to not favor it
			goto InitChanges;
		}

	Set:
		infos[name] = info;
		changes[name] = info;
		return;

	Init:
		_FieldInfos = infos = new();
	InitChanges:
		infos.Changes = changes = new();
		goto Set;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void DeleteFieldInfo(StringKey name)
		=> SetFieldInfo(name, default);

	/// <seealso cref="SetFieldInfoAsLoaded(StringKey, FieldInfo)"/>
	[SkipLocalsInit]
	public void SetCachedFieldInfo(StringKey name, FieldInfo info) {
		var infos = _FieldInfos;
		if (infos == null) {
			// This becomes a conditional jump forward to not favor it
			goto Init;
		}

	Set:
		infos[name] = info;

		{
			var changes = infos.Changes;
			// Optimized for the common case
			if (changes == null) {
				return;
			} else {
				ref var infoRef = ref CollectionsMarshal.GetValueRefOrNullRef(changes, name);
				if (!U.IsNullRef(ref infoRef)) {
					infoRef = info;
				}
				return;
			}
		}

	Init:
		_FieldInfos = infos = new();
		goto Set;
	}

	/// <summary>
	/// Same as <see cref="UnmarkFieldInfoAsChanged(StringKey)"/> followed by
	/// <see cref="SetCachedFieldInfo(StringKey, FieldInfo)"/>.
	/// </summary>
	public void SetFieldInfoAsLoaded(StringKey name, FieldInfo info) {
		var infos = _FieldInfos;
		if (infos == null) {
			// This becomes a conditional jump forward to not favor it
			goto Init;
		}

		infos.Changes?.Remove(name);

	Set:
		infos[name] = info;
		return;

	Init:
		_FieldInfos = infos = new();
		goto Set;
	}


	public void UnmarkFieldInfoAsChanged(StringKey name)
		=> _FieldInfos?.Changes?.Remove(name);

	public void UnmarkFieldInfosAsChanged()
		=> _FieldInfos?.Changes?.Clear();


	public void UnloadFieldInfo(StringKey name) {
		var infos = _FieldInfos;
		if (infos != null) {
			infos.Changes?.Remove(name);
			infos.Remove(name);
		}
	}

	public void UnloadFieldInfos() {
		var infos = _FieldInfos;
		if (infos != null) {
			infos.Changes = null;
			infos.Clear();
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
	private void InternalLoadFieldInfo(KokoroSqliteDb db, StringKey name) {
		long fld = db.LoadStaleNameId(name);
		if (fld == 0) goto NotFound;

		// Load field info
		using (var cmd = db.CreateCommand()) {
			cmd.Set(
				$"SELECT ord,sto,ifnull(enmGrp,0)enmGrp\n" +
				$"FROM {Prot.ClassToField}\n" +
				$"WHERE cls=$cls AND fld=$fld"
			).AddParams(
				new("$cls", _RowId),
				new("$fld", fld)
			);

			using var r = cmd.ExecuteReader();
			if (r.Read()) {
				r.DAssert_Name(0, "ord");
				int ordinal = r.GetInt32(0);

				r.DAssert_Name(1, "sto");
				var storeType = (FieldStoreType)r.GetByte(1);
				storeType.DAssert_Defined();

				r.DAssert_Name(2, "enmGrp");
				long enumGroupId = r.GetInt64(2);
				StringKey? enumGroup = enumGroupId == 0 ? null : db.LoadStaleName(enumGroupId);

				FieldInfo info = new(ordinal, enumGroup, storeType);

				// Pending changes will be discarded
				SetFieldInfoAsLoaded(name, info);
				return; // Early exit
			}
		}

	NotFound:
		// Otherwise, either deleted or never existed.
		// Let that state materialize here then.
		UnloadFieldInfo(name);
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
	private void InternalLoadFieldInfos(KokoroSqliteDb db) {
		db.ReloadNameIdCaches(); // Needed by `db.LoadStale…()` below

		using var cmd = db.CreateCommand();
		cmd.Set(
			$"SELECT ord,sto,ifnull(enmGrp,0)enmGrp,fld\n" +
			$"FROM {Prot.ClassToField}\n" +
			$"WHERE cls=$cls"
		).AddParams(new("$cls", _RowId));

		using var r = cmd.ExecuteReader();

		var infos = _FieldInfos;
		if (infos == null) {
			_FieldInfos = infos = new();
		} else {
			// Pending changes will be discarded
			infos.Changes = null;
			infos.Clear();
		}

		while (r.Read()) {
			r.DAssert_Name(0, "ord");
			int ordinal = r.GetInt32(0);

			r.DAssert_Name(1, "sto");
			var storeType = (FieldStoreType)r.GetByte(1);
			storeType.DAssert_Defined();

			r.DAssert_Name(2, "enmGrp");
			long enumGroupId = r.GetInt64(2);
			StringKey? enumGroup = enumGroupId == 0 ? null : db.LoadStaleName(enumGroupId);

			FieldInfo info = new(ordinal, enumGroup, storeType);

			r.DAssert_Name(3, "fld");
			long fld = r.GetInt64(3);
			var name = db.LoadStaleName(fld);
			Debug.Assert(name is not null, "An FK constraint should've been " +
				"enforced to ensure this doesn't happen.");

			infos[name] = info;
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
	private void InternalLoadFieldNames(KokoroSqliteDb db) {
		db.ReloadNameIdCaches(); // Needed by `db.LoadStale…()` below

		using var cmd = db.CreateCommand();
		cmd.Set($"SELECT fld FROM {Prot.ClassToField} WHERE cls=$cls")
			.AddParams(new("$cls", _RowId));

		using var r = cmd.ExecuteReader();
		while (r.Read()) {
			long fld = r.GetInt64(0);
			var name = db.LoadStaleName(fld);
			Debug.Assert(name is not null, "An FK constraint should've been " +
				"enforced to ensure this doesn't happen.");
			EnsureCachedFieldName(name);
		}
	}

	// --

	public void LoadFieldInfo(StringKey fieldName1) {
		var db = Host.Db;
		using (new OptionalReadTransaction(db)) {
			if (Exists) {
				db.ReloadNameIdCaches();
				InternalLoadFieldInfo(db, fieldName1);
			}
		}
	}

	public void LoadFieldInfo(StringKey fieldName1, StringKey fieldName2) {
		var db = Host.Db;
		using (new OptionalReadTransaction(db)) {
			if (Exists) {
				db.ReloadNameIdCaches();
				InternalLoadFieldInfo(db, fieldName1);
				InternalLoadFieldInfo(db, fieldName2);
			}
		}
	}

	public void LoadFieldInfo(StringKey fieldName1, StringKey fieldName2, StringKey fieldName3) {
		var db = Host.Db;
		using (new OptionalReadTransaction(db)) {
			if (Exists) {
				db.ReloadNameIdCaches();
				InternalLoadFieldInfo(db, fieldName1);
				InternalLoadFieldInfo(db, fieldName2);
				InternalLoadFieldInfo(db, fieldName3);
			}
		}
	}

	public void LoadFieldInfo(StringKey fieldName1, StringKey fieldName2, StringKey fieldName3, StringKey fieldName4) {
		var db = Host.Db;
		using (new OptionalReadTransaction(db)) {
			if (Exists) {
				db.ReloadNameIdCaches();
				InternalLoadFieldInfo(db, fieldName1);
				InternalLoadFieldInfo(db, fieldName2);
				InternalLoadFieldInfo(db, fieldName3);
				InternalLoadFieldInfo(db, fieldName4);
			}
		}
	}

	public void LoadFieldInfo(StringKey fieldName1, StringKey fieldName2, StringKey fieldName3, StringKey fieldName4, StringKey fieldName5) {
		var db = Host.Db;
		using (new OptionalReadTransaction(db)) {
			if (Exists) {
				db.ReloadNameIdCaches();
				InternalLoadFieldInfo(db, fieldName1);
				InternalLoadFieldInfo(db, fieldName2);
				InternalLoadFieldInfo(db, fieldName3);
				InternalLoadFieldInfo(db, fieldName4);
				InternalLoadFieldInfo(db, fieldName5);
			}
		}
	}

	public void LoadFieldInfo(StringKey fieldName1, StringKey fieldName2, StringKey fieldName3, StringKey fieldName4, StringKey fieldName5, StringKey fieldName6) {
		var db = Host.Db;
		using (new OptionalReadTransaction(db)) {
			if (Exists) {
				db.ReloadNameIdCaches();
				InternalLoadFieldInfo(db, fieldName1);
				InternalLoadFieldInfo(db, fieldName2);
				InternalLoadFieldInfo(db, fieldName3);
				InternalLoadFieldInfo(db, fieldName4);
				InternalLoadFieldInfo(db, fieldName5);
				InternalLoadFieldInfo(db, fieldName6);
			}
		}
	}

	public void LoadFieldInfo(StringKey fieldName1, StringKey fieldName2, StringKey fieldName3, StringKey fieldName4, StringKey fieldName5, StringKey fieldName6, StringKey fieldName7) {
		var db = Host.Db;
		using (new OptionalReadTransaction(db)) {
			if (Exists) {
				db.ReloadNameIdCaches();
				InternalLoadFieldInfo(db, fieldName1);
				InternalLoadFieldInfo(db, fieldName2);
				InternalLoadFieldInfo(db, fieldName3);
				InternalLoadFieldInfo(db, fieldName4);
				InternalLoadFieldInfo(db, fieldName5);
				InternalLoadFieldInfo(db, fieldName6);
				InternalLoadFieldInfo(db, fieldName7);
			}
		}
	}

	public void LoadFieldInfo(StringKey fieldName1, StringKey fieldName2, StringKey fieldName3, StringKey fieldName4, StringKey fieldName5, StringKey fieldName6, StringKey fieldName7, StringKey fieldName8) {
		var db = Host.Db;
		using (new OptionalReadTransaction(db)) {
			if (Exists) {
				db.ReloadNameIdCaches();
				InternalLoadFieldInfo(db, fieldName1);
				InternalLoadFieldInfo(db, fieldName2);
				InternalLoadFieldInfo(db, fieldName3);
				InternalLoadFieldInfo(db, fieldName4);
				InternalLoadFieldInfo(db, fieldName5);
				InternalLoadFieldInfo(db, fieldName6);
				InternalLoadFieldInfo(db, fieldName7);
				InternalLoadFieldInfo(db, fieldName8);
			}
		}
		// TODO A counterpart that loads up to 16 field infos
		// TODO Generate code via T4 text templates instead
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void LoadFieldInfo(params StringKey[] fieldNames)
		=> LoadFieldInfo(fieldNames.AsSpan());

	public void LoadFieldInfo(ReadOnlySpan<StringKey> fieldNames) {
		var db = Host.Db;
		using (new OptionalReadTransaction(db)) {
			if (Exists) {
				db.ReloadNameIdCaches();
				foreach (var fieldName in fieldNames)
					InternalLoadFieldInfo(db, fieldName);
			}
		}
	}

	public void LoadFieldInfos() {
		var db = Host.Db;
		using (new OptionalReadTransaction(db)) {
			if (Exists)
				InternalLoadFieldInfos(db);
		}
	}

	public void LoadFieldNames() {
		var db = Host.Db;
		using (new OptionalReadTransaction(db)) {
			if (Exists)
				InternalLoadFieldNames(db);
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
	private static void InternalSaveFieldInfos(KokoroSqliteDb db, Dictionary<StringKey, FieldInfo> changes, long clsId) {
		var changes_iter = changes.GetEnumerator();
		if (!changes_iter.MoveNext()) goto NoChanges;

		db.ReloadNameIdCaches(); // Needed by `db.LoadStale…()` below

		SqliteCommand?
			updCmd = null,
			delCmd = null;

		SqliteParameter
			cmd_cls = new("$cls", clsId),
			cmd_fld = new() { ParameterName = "$fld" };

		SqliteParameter
			updCmd_ord = null!,
			updCmd_sto = null!,
			updCmd_enmGrp = null!,
			updCmd_csum = null!;

		try {
		Loop:
			var (fieldName, info) = changes_iter.Current;
			long fld;
			if (info._IsLoaded) {
				fld = db.LoadStaleOrEnsureNameId(fieldName);
				if (updCmd != null) {
					goto UpdateFieldInfo;
				} else
					goto InitToUpdateFieldInfo;
			} else {
				fld = db.LoadStaleNameId(fieldName);
				if (fld != 0) {
					if (delCmd != null) {
						goto DeleteFieldInfo;
					} else
						goto InitToDeleteFieldInfo;
				} else {
					// Deletion requested, but there's nothing to delete, as the
					// field name is nonexistent.
					goto Continue;
				}
			}

		UpdateFieldInfo:
			{
				var hasher_fld = Blake2b.CreateIncrementalHasher(FieldInfoCsumDigestLength);
				/// WARNING: The expected order of inputs to be fed to the above
				/// hasher must be strictly as follows:
				///
				/// 0. `fieldName` in UTF8 with a (32-bit) length prepended
				/// 1. `info.Ordinal`
				/// 2. `info.StoreType`
				/// 3. `info.EnumGroup` in UTF8 with 1 plus the (32-bit)
				/// length prepended, or `0` if `info.EnumGroup` is null.
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
				///
				int hasher_fld_debug_i = 0; // Used only to help assert the above

				hasher_fld.UpdateWithLELength(fieldName.Value.ToUTF8Bytes());
				Debug.Assert(0 == hasher_fld_debug_i++);

				cmd_fld.Value = fld;

				updCmd_ord.Value = info.Ordinal;
				hasher_fld.UpdateLE(info.Ordinal);
				Debug.Assert(1 == hasher_fld_debug_i++);

				if (!info.StoreType.IsValid()) goto E_InvalidFieldStoreType;
				updCmd_sto.Value = (FieldStoreTypeInt)info.StoreType;
				Debug.Assert(sizeof(FieldStoreTypeInt) == 1);
				hasher_fld.UpdateLE((FieldStoreTypeInt)info.StoreType);
				Debug.Assert(2 == hasher_fld_debug_i++);

				var enumGroup = info.EnumGroup;
				if (enumGroup is null) {
					updCmd_enmGrp.Value = DBNull.Value;
					hasher_fld.UpdateLE(0);
				} else {
					updCmd_enmGrp.Value = db.LoadStaleOrEnsureNameId(enumGroup);
					hasher_fld.UpdateWithLELength(enumGroup.Value.ToUTF8Bytes(), lengthOffset: 1);
				}
				Debug.Assert(3 == hasher_fld_debug_i++);

				byte[] csum = FinishWithFieldInfoCsum(ref hasher_fld);
				updCmd_csum.Value = csum;

				int updated = updCmd.ExecuteNonQuery();
				Debug.Assert(updated == 1, $"Updated: {updated}");

				goto Continue;
			}

		DeleteFieldInfo:
			{
				cmd_fld.Value = fld;

				int deleted = delCmd.ExecuteNonQuery();
				// NOTE: It's possible for nothing to be deleted, for when the
				// field info didn't exist in the first place.
				Debug.Assert(deleted is 1 or 0, $"Deleted: {deleted}");

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

		InitToUpdateFieldInfo:
			{
				updCmd = db.CreateCommand();
				updCmd.Set(
					$"INSERT INTO {Prot.ClassToField}(cls,fld,csum,ord,sto,enmGrp)\n" +
					$"VALUES($cls,$fld,$csum,$ord,$sto,$enmGrp)\n" +
					$"ON CONFLICT DO UPDATE\n" +
					$"SET csum=$csum,ord=$ord,sto=$sto,enmGrp=$enmGrp"
				).AddParams(
					cmd_cls, cmd_fld,
					updCmd_ord = new() { ParameterName = "$ord" },
					updCmd_sto = new() { ParameterName = "$sto" },
					updCmd_enmGrp = new() { ParameterName = "$enmGrp" },
					updCmd_csum = new() { ParameterName = "$csum" }
				);
				Debug.Assert(
					cmd_cls.Value != null
				);
				goto UpdateFieldInfo;
			}

		InitToDeleteFieldInfo:
			{
				delCmd = db.CreateCommand();
				delCmd.Set(
					$"DELETE FROM {Prot.ClassToField} WHERE (cls,fld)=($cls,$fld)"
				).AddParams(
					cmd_cls, cmd_fld
				);
				Debug.Assert(
					cmd_cls.Value != null
				);
				goto DeleteFieldInfo;
			}

		Break:
			;

		} finally {
			updCmd?.Dispose();
			delCmd?.Dispose();
		}

	NoChanges:
		return;

	E_InvalidFieldStoreType:
		E_InvalidFieldStoreType(changes_iter.Current);

		[DoesNotReturn]
		static void E_InvalidFieldStoreType(KeyValuePair<StringKey, FieldInfo> current) {
			var (fieldName, info) = current;
			throw new InvalidOperationException(
				$"Invalid store type (currently {info.StoreType}) for field: {fieldName}");
		}
	}

	private const int FieldInfoCsumDigestLength = 31; // 248-bit hash

	private static byte[] FinishWithFieldInfoCsum(ref Blake2bHashState hasher) {
		const int CsumVer = 1; // The version varint
		const int CsumVerLength = 1; // The varint length is a single byte for now
		VarInts.DAssert_Equals(stackalloc byte[CsumVerLength] { CsumVer }, CsumVer);

		Span<byte> csum = stackalloc byte[CsumVerLength + FieldInfoCsumDigestLength];
		hasher.Finish(csum.Slice(CsumVerLength));

		// Prepend version varint
		csum[0] = CsumVer;

		// TODO In the future, once we're either using `sqlite3_stmt` directly or have replaced `Microsoft.Data.Sqlite`
		// with a custom version more suited to our needs, rent/stackalloc a buffer for the hash output instead, then
		// pass that as a `ReadOnlySpan<byte>` to `sqlite3_bind_blob()`.
		return csum.ToArray();
	}
}
