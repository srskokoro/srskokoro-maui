﻿namespace Kokoro;
using Blake2Fast;
using Blake2Fast.Implementation;
using Kokoro.Common.Util;
using Kokoro.Internal;
using Kokoro.Internal.Sqlite;
using Microsoft.Data.Sqlite;

/// <summary>
/// An entity class, i.e., a fielded entity's class.
/// </summary>
public sealed partial class Class : DataEntity {

	private long _RowId;

	private UniqueId _Uid;
	private byte[]? _CachedCsum;
	private long _ModStamp;

	private int _Ordinal;
	private long _ManagerId;
	private StringKey? _Name;


	public Class(KokoroCollection host) : base(host) { }

	public Class(KokoroCollection host, long rowid)
		: this(host) => _RowId = rowid;


	public long RowId {
		get => _RowId;
		set => _RowId = value;
	}

	public UniqueId Uid {
		get => _Uid;
		set {
			_Uid = value;
			_State |= StateFlags.Change_Uid;
		}
	}

	public void SetCachedUid(UniqueId uid) => _Uid = uid;

	public byte[]? CachedCsum => _CachedCsum;

	public void SetCachedCsum(byte[]? csum) => _CachedCsum = csum;

	public long ModStamp {
		get => _ModStamp;
		set {
			_ModStamp = value;
			_State |= StateFlags.Change_ModStamp;
		}
	}

	public void SetCachedModStamp(int modstamp) => _ModStamp = modstamp;


	public int Ordinal {
		get => _Ordinal;
		set {
			_Ordinal = value;
			_State |= StateFlags.Change_Ordinal;
		}
	}

	public void SetCachedOrdinal(int ordinal) => _Ordinal = ordinal;

	public long ManagerId {
		get => _ManagerId;
		set {
			_ManagerId = value;
			_State |= StateFlags.Change_ManagerId;
		}
	}

	public void SetCachedManagerId(long managerId) => _ManagerId = managerId;

	public StringKey? Name {
		get => _Name;
		set {
			_Name = value;
			_State |= StateFlags.Change_Name;
		}
	}

	public void SetCachedName(StringKey? name) => _Name = name;

	// --

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static long LoadRowId(KokoroCollection host, UniqueId uid)
		=> LoadRowId(host.Db, uid);

	internal static long LoadRowId(KokoroSqliteDb db, UniqueId uid) {
		using var cmd = db.CreateCommand();
		return cmd.Set($"SELECT rowid FROM {Prot.Class} WHERE uid=$uid")
			.AddParams(new("$uid", uid.ToByteArray()))
			.ExecScalarOrDefaultIfEmpty<long>();
	}


	[SkipLocalsInit]
	public void Load() {
		var db = Host.Db;
		using (new OptionalReadTransaction(db))
			InternalLoadCore(db);
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
	private void InternalLoadCore(KokoroSqliteDb db) {
		using var cmd = db.CreateCommand();
		cmd.Set(
			$"SELECT uid,csum,modst,ord," +
				$"ifnull(man,0)man," +
				$"ifnull(name,0)name\n" +
			$"FROM {Prot.Class}\n" +
			$"WHERE rowid=$rowid"
		).AddParams(new("$rowid", _RowId));

		using var r = cmd.ExecuteReader();
		if (r.Read()) {
			// Pending changes will be discarded
			_State = StateFlags.NoChanges;

			r.DAssert_Name(0, "uid");
			_Uid = r.GetUniqueId(0);

			r.DAssert_Name(1, "csum");
			_CachedCsum = r.GetBytes(1);

			r.DAssert_Name(2, "modst");
			_ModStamp = r.GetInt64(2);

			r.DAssert_Name(3, "ord");
			_Ordinal = r.GetInt32(3);

			r.DAssert_Name(4, "man");
			_ManagerId = r.GetInt64(4);

			r.DAssert_Name(5, "name");
			long nameId = r.GetInt64(5);
			_Name = nameId == 0 ? null
				: db.LoadName(nameId);

			return; // Early exit
		}

		// Otherwise, either deleted or never existed.
		Unload(); // Let that state materialize here then.
		_State = StateFlags.NotExists;
	}


	public struct LoadInfo {

		public bool Core { readonly get; set; }

		public bool AllFieldInfos { readonly get; set; }

		public bool AllFieldNames { readonly get; set; }
	}

	public void Load(LoadInfo loadInfo) {
		var db = Host.Db;
		using (new OptionalReadTransaction(db)) {
			if (loadInfo.Core) InternalLoadCore(db);
			if (Exists) {
				if (loadInfo.AllFieldInfos) InternalLoadFieldInfos(db);
				if (loadInfo.AllFieldNames) InternalLoadFieldNames(db);
			}
		}
	}


	public void Unload() {
		UnloadFieldInfos();
		UnloadCore();
	}

	public void UnloadCore() {
		_State = default;
		_Uid = default;
		_CachedCsum = default;
		_Ordinal = default;
		_ManagerId = default;
		_Name = default;
	}

	// --

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void SaveAsNew() => SaveAsNew(0);

	[SkipLocalsInit]
	public void SaveAsNew(long rowid) {
		var db = Host.Db; // Throws if host is already disposed

		bool hasUsedNextRowId;
		if (rowid == 0) {
			// Guaranteed not null if didn't throw above
			Debug.Assert(db.Context != null);
			rowid = db.Context.NextClassId();
			hasUsedNextRowId = true;
		} else {
			hasUsedNextRowId = false;
		}

		try {
			using var tx = new NestingWriteTransaction(db);

			// Save field infos
			// --
			{
				var fieldChanges = _FieldInfos?.Changes;
				if (fieldChanges != null)
					InternalSaveFieldInfos(db, fieldChanges, rowid);
			}

			// Save class includes
			// --
			{
				var includes = _Includes;
				if (includes != null && includes.Changes != null)
					InternalSaveIncludes(db, includes, rowid);
			}

			// Save enum groups
			// --
			{
				var enumGroupChanges = _EnumGroups?.Changes;
				if (enumGroupChanges != null)
					InternalSaveEnumGroups(db, enumGroupChanges, rowid);
			}

			// Save core state
			// --

			using var cmd = db.CreateCommand();
			cmd.Set(
				$"INSERT INTO {Prot.Class}" +
				$"(rowid,uid,csum,modst,ord,man,name)" +
				$"\nVALUES" +
				$"($rowid,$uid,$csum,$modst,$ord,$man,$name)"
			);

			var cmdParams = cmd.Parameters;
			cmdParams.Add(new("$rowid", rowid));

			var hasher = Blake2b.CreateIncrementalHasher(ClassCsumDigestLength);
			/// WARNING: The expected order of inputs to be fed to the above
			/// hasher has a strict format, which must be kept in sync with
			/// <see cref="SaveChanges"/> method (see that method to see the
			/// expected input format).
			int hasher_debug_i = 0; // Used only to help assert the above

			var state = _State;
			UniqueId uid;
			if ((state & StateFlags.Change_Uid) == 0) {
				uid = UniqueId.Create();
				_Uid = uid;
			} else {
				uid = _Uid;
			}
			cmdParams.Add(new("$uid", uid.ToByteArray()));
			hasher.Update(uid.Span);
			Debug.Assert(0 == hasher_debug_i++);

			cmdParams.Add(new("$ord", _Ordinal));
			hasher.UpdateLE(_Ordinal);
			Debug.Assert(1 == hasher_debug_i++);

			cmdParams.Add(new("$man", RowIds.DBBox(_ManagerId)));

			StringKey? name = _Name;
			cmdParams.Add(new("$name", name is null
				? DBNull.Value : db.EnsureNameId(name)));

			long modstamp;
			if ((state & StateFlags.Change_ModStamp) == 0) {
				modstamp = TimeUtils.UnixMillisNow();
				_ModStamp = modstamp;
			} else {
				modstamp = _ModStamp;
			}
			cmdParams.Add(new("$modst", modstamp));

			HashWithFieldInfos(db, rowid, ref hasher);
			Debug.Assert(2 == hasher_debug_i++);

			HashWithClassIncludes(db, rowid, ref hasher);
			Debug.Assert(3 == hasher_debug_i++);

			HashWithEnumInfos(db, rowid, ref hasher);
			Debug.Assert(4 == hasher_debug_i++);

			byte[] csum = FinishWithClassCsum(ref hasher);
			cmdParams.Add(new("$csum", csum));

			int updated = cmd.ExecuteNonQuery();
			Debug.Assert(updated == 1, $"Updated: {updated}");

			// COMMIT (or RELEASE) should be guaranteed to not fail at this
			// point if there's at least one operation that started a write.
			// - See, https://www.sqlite.org/rescode.html#busy
			tx.Commit();

			// Set new `csum` and clear pending changes (as they're now saved)
			// --
			{
				_CachedCsum = csum;
				_State = StateFlags.NoChanges;
				UnmarkFieldInfosAsChanged();
				UnmarkIncludesAsChanged();
			}
		} catch (Exception ex) when (hasUsedNextRowId && (
			ex is not SqliteException sqlex ||
			sqlex.SqliteExtendedErrorCode != SQLitePCL.raw.SQLITE_CONSTRAINT_ROWID
		)) {
			db.Context?.UndoClassId(rowid);
			throw;
		}

		_RowId = rowid;
	}

	[SkipLocalsInit]
	public void SaveChanges() {
		var state = _State;

		if (state < 0) goto Missing;
		Debug.Assert((StateFlags)(-1) < 0, $"Underlying type of `{nameof(StateFlags)}` must be signed");

		if (state == StateFlags.NoChanges) {
			var fieldInfos = _FieldInfos;
			if (fieldInfos != null) {
				var changes = fieldInfos.Changes;
				if (changes != null && changes.Count != 0) goto HasChanges;
			}

			var includes = _Includes;
			if (includes != null) {
				var changes = includes.Changes;
				if (changes != null && changes.Count != 0) goto HasChanges;
			}

			goto Success;

		HasChanges:
			;
		}

		var db = Host.Db; // Throws if host is already disposed
		using (var tx = new NestingWriteTransaction(db)) {
			long rowid = _RowId;

			// Save field infos
			// --
			{
				var fieldChanges = _FieldInfos?.Changes;
				if (fieldChanges != null)
					InternalSaveFieldInfos(db, fieldChanges, rowid);
			}

			// Save class includes
			// --
			{
				var includes = _Includes;
				if (includes != null && includes.Changes != null)
					InternalSaveIncludes(db, includes, rowid);
			}

			// Save enum groups
			// --
			{
				var enumGroupChanges = _EnumGroups?.Changes;
				if (enumGroupChanges != null)
					InternalSaveEnumGroups(db, enumGroupChanges, rowid);
			}

			// Save core state
			// --

			using var cmd = db.CreateCommand();
			var cmdParams = cmd.Parameters;
			cmdParams.Add(new("$rowid", rowid));

			StringBuilder cmdSb = new();
			cmdSb.Append($"UPDATE {Prot.Class} SET\n");

			var hasher = Blake2b.CreateIncrementalHasher(ClassCsumDigestLength);
			/// WARNING: The expected order of inputs to be fed to the above
			/// hasher must be strictly as follows:
			///
			/// 0. `uid`
			/// 1. `ord`
			/// 2. The 512-bit hash of, the list of `csum` data from `<see cref="Prot.ClassToField"/>`,
			/// ordered by `csum` -- see, https://crypto.stackexchange.com/q/54544
			/// 3. The 512-bit hash of, the list of `uid` data from `<see cref="Prot.ClassToInclude"/>`,
			/// ordered by `uid`
			/// 4. The 512-bit hash of, the list of `csum` data from `<see cref="Prot.ClassToEnum"/>`,
			/// ordered by `csum` -- see, https://crypto.stackexchange.com/q/54544
			///
			/// Unless stated otherwise, all integer inputs should be consumed
			/// in their little-endian form. <see href="https://en.wikipedia.org/wiki/Endianness"/>
			///
			/// The resulting hash BLOB shall be prepended with a version
			/// varint. Should any of the following happens, the version varint
			/// must change:
			///
			/// - The resulting hash BLOB length changes.
			/// - The algorithm for the resulting hash BLOB changes.
			/// - An input entry (from the list of inputs above) was removed.
			/// - The order of an input entry (from the list of inputs above)
			/// was changed or shifted.
			/// - An input entry's size (in bytes) changed while it's expected
			/// to be fixed-sized (e.g., not length-prepended).
			///
			/// The version varint needs not to change if further input entries
			/// were to be appended (from the list of inputs above), provided
			/// that the last input entry has a clear termination, i.e., fixed-
			/// sized or length-prepended.
			///
			int hasher_debug_i = 0; // Used only to help assert the above

			// TODO Avoid creating this when it won't be used at all
			using var selOldCmd = db.CreateCommand();
			selOldCmd.Set(
				$"SELECT uid,ord FROM {Prot.Class}\n" +
				$"WHERE rowid=$rowid"
			).AddParams(new("$rowid", rowid));

			using var r = selOldCmd.ExecuteReader();
			if (!r.Read()) goto Missing;

			if ((state & StateFlags.Change_Uid) == 0) {
				r.DAssert_Name(0, "uid");
				_Uid = r.GetUniqueId(0);
			} else {
				// NOTE: Changing the UID should be considerd similar to
				// removing an entry with the old UID, then recreating that
				// entry with a new UID, except that any creation timestamping
				// mechanism (that is, if any) isn't disturbed.
				cmdSb.Append("uid=$uid,");
				cmdParams.Add(new("$uid", _Uid.ToByteArray()));
				// TODO Create graveyard entry for the old UID to assist with syncing
				// - The modstamp of the graveyard entry should be equal to or
				// less than the class's modstamp being saved in the current
				// operation.
			}
			hasher.Update(_Uid.Span);
			Debug.Assert(0 == hasher_debug_i++);

			if ((state & StateFlags.Change_Ordinal) == 0) {
				r.DAssert_Name(1, "ord");
				Debug.Assert(Types.TypeOf(_Ordinal) == typeof(int));
				_Ordinal = r.GetInt32(1);
			} else {
				cmdSb.Append("ord=$ord,");
				cmdParams.Add(new("$ord", _Ordinal));
			}
			hasher.UpdateLE(_Ordinal);
			Debug.Assert(1 == hasher_debug_i++);

			// --
			{
				if (state == StateFlags.NoChanges) goto ModStampIsNow;

				if ((state & StateFlags.Change_ManagerId) != 0) {
					cmdSb.Append("man=$man,");
					cmdParams.Add(new("$man", RowIds.DBBox(_ManagerId)));
				}
				if ((state & StateFlags.Change_Name) != 0) {
					cmdSb.Append("name=$name,");

					StringKey? name = _Name;
					cmdParams.Add(new("$name", name is null
						? DBNull.Value : db.EnsureNameId(name)));
				}

				if ((state & StateFlags.Change_ModStamp) != 0) {
					goto ModStampIsCustom;
				}

			ModStampIsNow:
				long modstamp = TimeUtils.UnixMillisNow();
				_ModStamp = modstamp;
				goto SetModStampParam;

			ModStampIsCustom:
				modstamp = _ModStamp;
				goto SetModStampParam;

			SetModStampParam:
				cmdParams.Add(new("$modst", modstamp));
				cmdSb.Append("modst=$modst,");
			}

			HashWithFieldInfos(db, rowid, ref hasher);
			Debug.Assert(2 == hasher_debug_i++);

			HashWithClassIncludes(db, rowid, ref hasher);
			Debug.Assert(3 == hasher_debug_i++);

			HashWithEnumInfos(db, rowid, ref hasher);
			Debug.Assert(4 == hasher_debug_i++);

			byte[] csum = FinishWithClassCsum(ref hasher);
			cmdParams.Add(new("$csum", csum));

			const string CmdSbEnd = "csum=$csum WHERE rowid=$rowid";
			cmdSb.Append(CmdSbEnd);

			cmd.CommandText = cmdSb.ToString();

			int updated = cmd.ExecuteNonQuery();
			if (updated != 0) {
				Debug.Assert(updated == 1, $"Updated: {updated}");
				// COMMIT (or RELEASE) should be guaranteed to not fail at this
				// point if there's at least one operation that started a write.
				// - See, https://www.sqlite.org/rescode.html#busy
				tx.Commit();
			} else {
				goto Missing;
			}

			// Set new `csum` and clear pending changes (as they're now saved)
			// --
			{
				_CachedCsum = csum;
				_State = StateFlags.NoChanges;
				UnmarkFieldInfosAsChanged();
				UnmarkIncludesAsChanged();
			}
		}

	Success:
		return;

	Missing:
		E_CannotUpdate_MRec(_RowId);

		[DoesNotReturn]
		static void E_CannotUpdate_MRec(long rowid) => throw new MissingRecordException(
			$"Cannot update `{nameof(Class)}` with rowid {rowid} as it's missing.");
	}

	private static void HashWithFieldInfos(KokoroSqliteDb db, long cls, ref Blake2bHashState hasher) {
		const int hasher_flds_dlen = 64; // 512-bit hash
		var hasher_flds = Blake2b.CreateIncrementalHasher(hasher_flds_dlen);

		using var cmd = db.CreateCommand();
		cmd.Set(
			$"SELECT csum\n" +
			$"FROM {Prot.ClassToField}\n" +
			$"WHERE cls=$cls\n" +
			$"ORDER BY csum" // See, https://crypto.stackexchange.com/q/54544
		).AddParams(new("$cls", cls));

		using var r = cmd.ExecuteReader();
		while (r.Read()) {
			r.DAssert_Name(0, "csum");
			// NOTE: Will be an empty span on null (i.e., won't throw NRE)
			var csum = (ReadOnlySpan<byte>)r.GetBytesOrNull(0);
			Debug.Assert(csum.Length > 0, $"Unexpected: field info has no `csum` (under class rowid {cls})");
			hasher_flds.Update(csum);
		}

		hasher.Update(hasher_flds.FinishAndGet(stackalloc byte[hasher_flds_dlen]));
	}

	private static void HashWithClassIncludes(KokoroSqliteDb db, long cls, ref Blake2bHashState hasher) {
		const int hasher_incls_dlen = 64; // 512-bit hash
		var hasher_incls = Blake2b.CreateIncrementalHasher(hasher_incls_dlen);

		using var cmd = db.CreateCommand();
		cmd.Set(
			$"SELECT uid FROM {Prot.ClassToInclude}\n" +
			$"WHERE cls=$cls\n" +
			$"ORDER BY uid"
		).AddParams(new("$cls", cls));

		using var r = cmd.ExecuteReader();
		while (r.Read()) {
			r.DAssert_Name(0, "uid");
			UniqueId uid = r.GetUniqueId(0);
			hasher_incls.Update(uid.Span);
		}

		hasher.Update(hasher_incls.FinishAndGet(stackalloc byte[hasher_incls_dlen]));
	}

	private static void HashWithEnumInfos(KokoroSqliteDb db, long cls, ref Blake2bHashState hasher) {
		const int hasher_enums_dlen = 64; // 512-bit hash
		var hasher_enums = Blake2b.CreateIncrementalHasher(hasher_enums_dlen);

		using var cmd = db.CreateCommand();
		cmd.Set(
			$"SELECT csum\n" +
			$"FROM {Prot.ClassToEnum}\n" +
			$"WHERE cls=$cls\n" +
			$"ORDER BY csum" // See, https://crypto.stackexchange.com/q/54544
		).AddParams(new("$cls", cls));

		using var r = cmd.ExecuteReader();
		while (r.Read()) {
			r.DAssert_Name(0, "csum");
			// NOTE: Will be an empty span on null (i.e., won't throw NRE)
			var csum = (ReadOnlySpan<byte>)r.GetBytesOrNull(0);
			Debug.Assert(csum.Length > 0, $"Unexpected: enum info has no `csum` (under class rowid {cls})");
			hasher_enums.Update(csum);
		}

		hasher.Update(hasher_enums.FinishAndGet(stackalloc byte[hasher_enums_dlen]));
	}

	private const int ClassCsumDigestLength = 31; // 248-bit hash

	private static byte[] FinishWithClassCsum(ref Blake2bHashState hasher) {
		const int CsumVer = 1; // The version varint
		const int CsumVerLength = 1; // The varint length is a single byte for now
		VarInts.DAssert_Equals(stackalloc byte[CsumVerLength] { CsumVer }, CsumVer);

		Span<byte> csum = stackalloc byte[CsumVerLength + ClassCsumDigestLength];
		hasher.Finish(csum.Slice(CsumVerLength));

		// Prepend version varint
		csum[0] = CsumVer;

		// TODO In the future, once we're either using `sqlite3_stmt` directly or have replaced `Microsoft.Data.Sqlite`
		// with a custom version more suited to our needs, rent/stackalloc a buffer for the hash output instead, then
		// pass that as a `ReadOnlySpan<byte>` to `sqlite3_bind_blob()`.
		return csum.ToArray();
	}

	// --

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void RenewRowId(KokoroCollection host, long oldRowId)
		=> AlterRowId(host, oldRowId, 0);

	/// <summary>
	/// Alias for <see cref="RenewRowId(KokoroCollection, long)"/>
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void AlterRowId(KokoroCollection host, long oldRowId)
		=> RenewRowId(host, oldRowId);

	[SkipLocalsInit]
	public static void AlterRowId(KokoroCollection host, long oldRowId, long newRowId) {
		var db = host.Db; // Throws if host is already disposed

		bool hasUsedNextRowId;
		if (newRowId == 0) {
			// Guaranteed not null if didn't throw above
			Debug.Assert(db.Context != null);
			newRowId = db.Context.NextClassId();
			hasUsedNextRowId = true;
		} else {
			hasUsedNextRowId = false;
		}

		int updated;
		try {
			using var cmd = db.CreateCommand();
			updated = cmd.Set($"UPDATE {Prot.Class} SET rowid=$newRowId WHERE rowid=$oldRowId")
				.AddParams(new("$oldRowId", oldRowId), new("$newRowId", newRowId))
				.Exec();
		} catch (Exception ex) when (hasUsedNextRowId && (
			ex is not SqliteException sqlex ||
			sqlex.SqliteExtendedErrorCode != SQLitePCL.raw.SQLITE_CONSTRAINT_ROWID
		)) {
			db.Context?.UndoClassId(newRowId);
			throw;
		}

		if (updated != 0) {
			Debug.Assert(updated == 1, $"Updated: {updated}");
			return;
		}

		// Otherwise, record missing -- either deleted or never existed.
		E_CannotUpdate_MRec(oldRowId);

		[DoesNotReturn]
		static void E_CannotUpdate_MRec(long rowid) => throw new MissingRecordException(
			$"Cannot update `{nameof(Class)}` with rowid {rowid} as it's missing.");
	}


	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool Delete() => DeleteFrom(Host, _RowId);

	public static bool DeleteFrom(KokoroCollection host, long rowid) {
		var db = host.Db;

		int deleted;
		using (var cmd = db.CreateCommand()) {
			deleted = cmd.Set($"DELETE FROM {Prot.Class} WHERE rowid=$rowid")
				.AddParams(new("$rowid", rowid)).Exec();

			// TODO Create graveyard entry for the deleted UID to assist with syncing
			// - Place in a write transaction, as necessary.
			// - Also provide a parameter to customize the graveyard entry's
			// modstamp.
			//   - Perhaps if nothing was deleted, and there's a custom modstamp
			//   given, look up for any existing graveyard entry and update its
			//   modstamp to the given custom modstamp.
		}

		Debug.Assert(deleted is 1 or 0, $"Deleted: {deleted}");
		return ((byte)deleted).ToUnsafeBool();
	}

	public static bool DeleteFrom(KokoroCollection host, UniqueId uid) {
		var db = host.Db;

		int deleted;
		using (var cmd = db.CreateCommand()) {
			deleted = cmd.Set($"DELETE FROM {Prot.Class} WHERE uid=$uid")
				.AddParams(new("$uid", uid)).Exec();

			// TODO Create graveyard entry for the deleted UID to assist with syncing
			// - Place in a write transaction, as necessary.
			// - Also provide a parameter to customize the graveyard entry's
			// modstamp.
			//   - Perhaps if nothing was deleted, and there's a custom modstamp
			//   given, look up for any existing graveyard entry and update its
			//   modstamp to the given custom modstamp.
		}

		Debug.Assert(deleted is 1 or 0, $"Deleted: {deleted}");
		return ((byte)deleted).ToUnsafeBool();
	}
}
