namespace Kokoro;
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
	private long _GroupId;
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
			_State = StateFlags.Change_Uid;
		}
	}

	public void SetCachedUid(UniqueId uid) => _Uid = uid;

	public byte[]? CachedCsum => _CachedCsum;

	public void SetCachedCsum(byte[]? csum) => _CachedCsum = csum;

	public long ModStamp {
		get => _ModStamp;
		set {
			_ModStamp = value;
			_State = StateFlags.Change_ModStamp;
		}
	}

	public void SetCachedModStamp(int modstamp) => _ModStamp = modstamp;


	public int Ordinal {
		get => _Ordinal;
		set {
			_Ordinal = value;
			_State = StateFlags.Change_Ordinal;
		}
	}

	public void SetCachedOrdinal(int ordinal) => _Ordinal = ordinal;

	public long GroupId {
		get => _GroupId;
		set {
			_GroupId = value;
			_State = StateFlags.Change_GroupId;
		}
	}

	public void SetCachedGroupId(long groupId) => _GroupId = groupId;

	public StringKey? Name {
		get => _Name;
		set {
			_Name = value;
			_State = StateFlags.Change_Name;
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
			.ExecScalarOrDefault<long>();
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
				$"ifnull(grp,0)AS grp," +
				$"ifnull(name,0)AS name\n" +
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

			r.DAssert_Name(4, "grp");
			_GroupId = r.GetInt64(4);

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
		_GroupId = default;
		_Name = default;
	}

	// --

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void SaveAsNew() => SaveAsNew(UniqueId.Create());

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void SaveAsNew(UniqueId uid) => SaveAsNew(0, uid);

	[SkipLocalsInit]
	public void SaveAsNew(long rowid, UniqueId uid) {
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
				var fieldChanges = _FieldInfos?._Changes;
				if (fieldChanges != null)
					InternalSaveFieldInfos(db, fieldChanges, rowid);
			}

			// Save class includes
			// --
			{
				var includes = _Includes;
				if (includes != null && includes._Changes != null)
					InternalSaveIncludes(db, includes, rowid);
			}

			// Save core state
			// --

			using var cmd = db.CreateCommand();
			cmd.Set(
				$"INSERT INTO {Prot.Class}" +
				$"(rowid,uid,csum,modst,ord,grp,name)" +
				$"\nVALUES" +
				$"($rowid,$uid,$csum,$modst,$ord,$grp,$name)"
			);

			var cmdParams = cmd.Parameters;
			cmdParams.Add(new("$rowid", rowid));

			var hasher = Blake2b.CreateIncrementalHasher(ClassCsumDigestLength);
			/// WARNING: The expected order of inputs to be fed to the above
			/// hasher has a strict format, which must be kept in sync with
			/// <see cref="SaveChanges"/> method (see that method to see the
			/// expected input format).
			int hasher_debug_i = 0; // Used only to help assert the above

			cmdParams.Add(new("$uid", uid.ToByteArray()));
			hasher.Update(uid.Span);
			Debug.Assert(0 == hasher_debug_i++);

			cmdParams.Add(new("$ord", _Ordinal));
			hasher.UpdateLE(_Ordinal);
			Debug.Assert(1 == hasher_debug_i++);

			cmdParams.Add(new("$grp", RowIds.DBBox(_GroupId)));

			StringKey? name = _Name;
			cmdParams.Add(new("$name", name is null
				? DBNull.Value : db.EnsureNameId(name)));

			HashWithFieldInfos(db, rowid, ref hasher);
			Debug.Assert(2 == hasher_debug_i++);

			HashWithClassIncludes(db, rowid, ref hasher);
			Debug.Assert(3 == hasher_debug_i++);

			byte[] csum = FinishWithClassCsum(ref hasher);
			cmdParams.Add(new("$csum", csum));

			long modstamp = (_State & StateFlags.Change_ModStamp) != 0 ? _ModStamp : TimeUtils.UnixMillisNow();
			cmdParams.Add(new("$modst", modstamp));

			int updated = cmd.ExecuteNonQuery();
			Debug.Assert(updated == 1, $"Updated: {updated}");

			// COMMIT (or RELEASE) should be guaranteed to not fail at this
			// point if there's at least one operation that started a write.
			// - See, https://www.sqlite.org/rescode.html#busy
			tx.Commit();

			// Conclude modstamp, set new `csum`, and clear pending changes (as
			// they're now saved)
			// --
			{
				_ModStamp = modstamp;
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
		_Uid = uid;
	}

	[SkipLocalsInit]
	public void SaveChanges() {
		var state = _State;
		if (state < 0) goto Missing;
		if (state == StateFlags.NoChanges) {
			var infos = _FieldInfos;
			if (infos == null) goto Success;
			if (infos._Changes == null) goto Success;
		}

		var db = Host.Db; // Throws if host is already disposed
		using (var tx = new NestingWriteTransaction(db)) {
			long rowid = _RowId;

			// Save field infos
			// --
			{
				var fieldChanges = _FieldInfos?._Changes;
				if (fieldChanges != null)
					InternalSaveFieldInfos(db, fieldChanges, rowid);
			}

			// Save class includes
			// --
			{
				var includes = _Includes;
				if (includes != null && includes._Changes != null)
					InternalSaveIncludes(db, includes, rowid);
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
			/// 2. The 512-bit hash of, the list of `csum` data from `ClassToField`,
			/// ordered by `ClassToField.ord,NameId.name`
			/// 3. The 512-bit hash of, the list of `uid` data from `<see cref="Prot.Class"/> ON <see cref="Prot.Class"/>.rowid=ClassToInclude.incl`,
			/// ordered by `<see cref="Prot.Class"/>.uid`
			///
			/// Unless stated otherwise, all integer inputs should be consumed
			/// in their little-endian form.
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
			/// to be fixed-size (e.g., not length-prepended).
			///
			/// The version varint needs not to change if further input entries
			/// were to be appended (from the list of inputs above).
			///
			int hasher_debug_i = 0; // Used only to help assert the above

			// TODO Avoid creating this when it won't be used at all
			using var cmd_old = db.CreateCommand();
			cmd_old.Set(
				$"SELECT uid,ord FROM {Prot.Class}\n" +
				$"WHERE rowid=$rowid"
			).AddParams(new("$rowid", rowid));

			using var r = cmd_old.ExecuteReader();
			if (!r.Read()) goto Missing;

			if ((state & StateFlags.Change_Uid) == 0) {
				r.DAssert_Name(0, "uid");
				hasher.Update(r.GetUniqueId(0).Span);
				Debug.Assert(0 == hasher_debug_i++);
			} else {
				cmdSb.Append("uid=$uid,");
				cmdParams.Add(new("$uid", _Uid.ToByteArray()));
				hasher.Update(_Uid.Span);
				Debug.Assert(0 == hasher_debug_i++);
			}

			if ((state & StateFlags.Change_Ordinal) == 0) {
				r.DAssert_Name(1, "ord");
				Debug.Assert(Types.TypeOf(_Ordinal) == typeof(int));
				hasher.UpdateLE(r.GetInt32(1));
				Debug.Assert(1 == hasher_debug_i++);
			} else {
				cmdSb.Append("ord=$ord,");
				cmdParams.Add(new("$ord", _Ordinal));
				hasher.UpdateLE(_Ordinal);
				Debug.Assert(1 == hasher_debug_i++);
			}

			long modstamp;

			// --
			{
				if (state == StateFlags.NoChanges) goto ModStampIsNow;

				if ((state & StateFlags.Change_GroupId) != 0) {
					cmdSb.Append("grp=$grp,");
					cmdParams.Add(new("$grp", RowIds.DBBox(_GroupId)));
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
				modstamp = TimeUtils.UnixMillisNow();
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

			// Conclude modstamp, set new `csum`, and clear pending changes (as
			// they're now saved)
			// --
			{
				_ModStamp = modstamp;
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
			$"SELECT cls2fld.csum AS csum\n" +
			$"FROM ClassToField AS cls2fld,NameId AS fld\n" +
			$"WHERE cls2fld.cls=$cls AND fld.rowid=cls2fld.fld\n" +
			$"ORDER BY cls2fld.ord,fld.name"
		).AddParams(new("$cls", cls));

		using var r = cmd.ExecuteReader();
		while (r.Read()) {
			r.DAssert_Name(0, "csum");
			// Will be empty span on null (i.e., won't throw NRE)
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
			$"SELECT cls.uid AS uid FROM ClassToInclude AS cls2incl,{Prot.Class} AS cls\n" +
			$"WHERE cls2incl.cls=$cls AND cls.rowid=cls2incl.incl\n" +
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

	private const int ClassCsumDigestLength = 31; // 248-bit hash

	private static byte[] FinishWithClassCsum(ref Blake2bHashState hasher) {
		const int CsumVer = 1; // The version varint
		const int CsumVerLength = 1; // The varint length is a single byte for now
		Debug.Assert(VarInts.Length(CsumVer) == CsumVerLength);

		Span<byte> csum = stackalloc byte[CsumVerLength + ClassCsumDigestLength];
		csum[0] = CsumVer; // Prepend version varint
		hasher.Finish(csum[CsumVerLength..]);
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
		}

		Debug.Assert(deleted is 1 or 0);
		return ((byte)deleted).ToUnsafeBool();
	}

	public static bool DeleteFrom(KokoroCollection host, UniqueId uid) {
		var db = host.Db;

		int deleted;
		using (var cmd = db.CreateCommand()) {
			deleted = cmd.Set($"DELETE FROM {Prot.Class} WHERE uid=$uid")
				.AddParams(new("$uid", uid)).Exec();
		}

		Debug.Assert(deleted is 1 or 0);
		return ((byte)deleted).ToUnsafeBool();
	}
}
