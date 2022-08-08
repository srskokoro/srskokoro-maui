namespace Kokoro;
using Blake2Fast;
using Blake2Fast.Implementation;
using Kokoro.Common.Util;
using Kokoro.Internal.Sqlite;
using Microsoft.Data.Sqlite;
using System.Runtime.InteropServices;

partial class Class {

	private FieldInfos? _FieldInfos;

	private sealed class FieldInfos : Dictionary<StringKey, FieldInfo> {
		internal FieldInfoChanges? _Changes;
	}

	private sealed class FieldInfoChanges : Dictionary<StringKey, FieldInfo> { }


	public readonly struct FieldInfo {
		internal readonly bool _IsLoaded;

		private readonly int _Ordinal;
		private readonly FieldStoreType _StoreType;

		public readonly int Ordinal => _Ordinal;
		public readonly FieldStoreType StoreType => _StoreType;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FieldInfo(int ordinal, FieldStoreType storeType) {
			_IsLoaded = true;
			_Ordinal = ordinal;
			_StoreType = storeType;
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

		var changes = infos._Changes;
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
		infos._Changes = changes = new();
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
			var changes = infos._Changes;
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

		infos._Changes?.Remove(name);

	Set:
		infos[name] = info;
		return;

	Init:
		_FieldInfos = infos = new();
		goto Set;
	}


	public void UnmarkFieldInfoAsChanged(StringKey name)
		=> _FieldInfos?._Changes?.Remove(name);

	public void UnmarkFieldInfosAsChanged()
		=> _FieldInfos?._Changes?.Clear();


	public void UnloadFieldInfo(StringKey name) {
		var infos = _FieldInfos;
		if (infos != null) {
			infos._Changes?.Remove(name);
			infos.Remove(name);
		}
	}

	public void UnloadFieldInfos() {
		var infos = _FieldInfos;
		if (infos != null) {
			infos._Changes = null;
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
				"SELECT ord,sto FROM ClassToField\n" +
				"WHERE cls=$cls AND fld=$fld"
			).AddParams(
				new("$cls", _RowId),
				new("$fld", fld)
			);

			using var r = cmd.ExecuteReader();
			if (r.Read()) {
				r.DAssert_Name(0, "ord");
				int ordinal = r.GetInt32(0);

				r.DAssert_Name(1, "sto");
				var storeType = (FieldStoreType)r.GetInt32(1);
				storeType.DAssert_Defined();

				FieldInfo info = new(ordinal, storeType);

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
		cmd.Set("SELECT ord,sto,fld FROM ClassToField WHERE cls=$cls")
			.AddParams(new() { ParameterName = "$cls" });

		using var r = cmd.ExecuteReader();
		while (r.Read()) {
			r.DAssert_Name(0, "ord");
			int ordinal = r.GetInt32(0);

			r.DAssert_Name(1, "sto");
			var storeType = (FieldStoreType)r.GetInt32(1);
			storeType.DAssert_Defined();

			FieldInfo info = new(ordinal, storeType);

			r.DAssert_Name(2, "fld");
			long fld = r.GetInt64(2);
			var name = db.LoadStaleName(fld);
			Debug.Assert(name is not null, "An FK constraint should've been " +
				"enforced to ensure this doesn't happen.");

			// Pending changes will be discarded
			SetFieldInfoAsLoaded(name, info);
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
		cmd.Set("SELECT fld FROM ClassToField WHERE cls=$cls")
			.AddParams(new() { ParameterName = "$cls" });

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
			updCmd_csum = null!;

		try {
			do {
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
						// Deletion requested, but there's nothing to delete, as
						// the field is nonexistent.
						continue;
					}
				}

			InitToUpdateFieldInfo:
				{
					updCmd = db.CreateCommand();
					updCmd.Set(
						"INSERT INTO ClassToField(cls,fld,csum,ord,sto)\n" +
						"VALUES($cls,$fld,$csum,$ord,$sto)\n" +
						"ON CONFLICT DO UPDATE\n" +
						"SET csum=$csum,ord=$ord,sto=$sto"
					).AddParams(
						cmd_cls, cmd_fld,
						updCmd_ord = new() { ParameterName = "$ord" },
						updCmd_sto = new() { ParameterName = "$sto" },
						updCmd_csum = new() { ParameterName = "$csum" }
					);
					Debug.Assert(
						cmd_cls.Value != null
					);
					goto UpdateFieldInfo;
				}

			UpdateFieldInfo:
				{
					var hasher_fld = Blake2b.CreateIncrementalHasher(FieldInfoCsumDigestLength);
					// WARNING: The expected order of inputs to be fed to the
					// above hasher must be strictly as follows:
					//
					// 0. `fieldName` in UTF8 with length prepended
					// 1. `info.Ordinal`
					// 2. `info.StoreType`
					//
					// Unless stated otherwise, all integer inputs should be
					// consumed in their little-endian form.
					//
					// The resulting hash BLOB shall be prepended with a version
					// varint. Should any of the following happens, the version
					// varint must change:
					//
					// - The resulting hash BLOB length changes.
					// - The algorithm for the resulting hash BLOB changes.
					// - An input entry (from the list of inputs above) was
					// removed.
					// - The order of an input entry (from the list of inputs
					// above) was changed or shifted.
					// - An input entry's size (in bytes) changed while it's
					// expected to be fixed-size (e.g., not length-prepended).
					//
					// The version varint needs not to change if further input
					// entries were to be appended (from the list of inputs
					// above).
					//
					int hasher_fld_debug_i = 0; // Used only to help assert the above

					hasher_fld.UpdateWithLELength(fieldName.Value.ToUTF8Bytes());
					Debug.Assert(0 == hasher_fld_debug_i++);

					cmd_fld.Value = fld;

					updCmd_ord.Value = info.Ordinal;
					hasher_fld.UpdateLE(info.Ordinal);
					Debug.Assert(1 == hasher_fld_debug_i++);

					updCmd_sto.Value = info.StoreType;
					Debug.Assert(sizeof(FieldStoreTypeInt) == 4);
					hasher_fld.UpdateLE((FieldStoreTypeInt)info.StoreType);
					Debug.Assert(2 == hasher_fld_debug_i++);

					byte[] csum = FinishWithFieldInfoCsum(ref hasher_fld);
					updCmd_csum.Value = csum;

					int updated = updCmd.ExecuteNonQuery();
					Debug.Assert(updated == 1, $"Updated: {updated}");

					continue;
				}

			InitToDeleteFieldInfo:
				{
					delCmd = db.CreateCommand();
					delCmd.Set(
						"DELETE FROM ClassToField WHERE (cls,fld)=($cls,$fld)"
					).AddParams(
						cmd_cls, cmd_fld
					);
					Debug.Assert(
						cmd_cls.Value != null
					);
					goto DeleteFieldInfo;
				}

			DeleteFieldInfo:
				{
					cmd_fld.Value = fld;

					int deleted = delCmd.ExecuteNonQuery();
					// NOTE: It's possible for nothing to be deleted, for when
					// the field info didn't exist in the first place.
					Debug.Assert(deleted is 1 or 0);

					continue;
				}

			} while (changes_iter.MoveNext());

		} finally {
			updCmd?.Dispose();
			delCmd?.Dispose();
		}

	NoChanges:
		;
	}

	private const int FieldInfoCsumDigestLength = 31; // 248-bit hash

	private static byte[] FinishWithFieldInfoCsum(ref Blake2bHashState hasher) {
		const int CsumVer = 1; // The version varint
		const int CsumVerLen = 1; // The varint length is a single byte for now
		Debug.Assert(VarInts.Length(CsumVer) == CsumVerLen);

		Span<byte> csum = stackalloc byte[CsumVerLen + FieldInfoCsumDigestLength];
		csum[0] = CsumVer; // Prepend version varint
		hasher.Finish(csum[CsumVerLen..]);
		return csum.ToArray();
	}
}
