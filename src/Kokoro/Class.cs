namespace Kokoro;
using Blake2Fast;
using Blake2Fast.Implementation;
using Kokoro.Common.IO;
using Kokoro.Common.Util;
using Kokoro.Internal;
using Kokoro.Internal.Sqlite;
using Microsoft.Data.Sqlite;
using System.Runtime.InteropServices;

/// <summary>
/// An entity class, i.e., a fielded entity's class.
/// </summary>
public sealed class Class : DataEntity {

	private long _RowId;

	private UniqueId _Uid;
	private byte[]? _CachedCsum;

	private int _Ordinal;
	private long _GrpRowId;
	private string? _Name;

	private Dictionary<StringKey, FieldInfo>? _FieldInfos;
	private Dictionary<StringKey, FieldInfo>? _FieldInfoChanges;

	private StateFlags _State;

	public bool Exists {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		// Ternary operator returning true/false prevents redundant asm generation:
		// See, https://github.com/dotnet/runtime/issues/4207#issuecomment-147184273
		get => _State < 0 ? false : true;
	}


	[Flags]
	private enum StateFlags : int {
		NoChanges = 0,

		Change_Uid      = 1 << 0,
		Change_Ordinal  = 1 << 1,
		Change_GrpRowId = 1 << 2,
		Change_Name     = 1 << 3,

		NotExists       = 1 << 31,
	}

	public struct FieldInfo {
		internal bool _IsDeleted;

		private int _Ordinal;
		private FieldStorageType _StorageType;

		private StringKey? _AliasTarget;

		public int Ordinal {
			readonly get => _Ordinal;
			set => _Ordinal = value;
		}

		public FieldStorageType StorageType {
			readonly get => _StorageType;
			set => _StorageType = value;
		}

		/// <remarks>
		/// If nonnull, <see cref="StorageType"/> can be any value.
		/// </remarks>
		public StringKey? AliasTarget {
			readonly get => _AliasTarget;
			set => _AliasTarget = value;
		}
	}


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


	public int Ordinal {
		get => _Ordinal;
		set {
			_Ordinal = value;
			_State = StateFlags.Change_Ordinal;
		}
	}

	public void SetCachedOrdinal(int ordinal) => _Ordinal = ordinal;

	public long GrpRowId {
		get => _GrpRowId;
		set {
			_GrpRowId = value;
			_State = StateFlags.Change_GrpRowId;
		}
	}

	public void SetCachedGrpRowId(long grpRowId) => _GrpRowId = grpRowId;

	public string? Name {
		get => _Name;
		set {
			_Name = value;
			_State = StateFlags.Change_Name;
		}
	}

	public void SetCachedName(string? name) => _Name = name;


	public bool TryGetFieldInfo(StringKey name, [MaybeNullWhen(false)] out FieldInfo info) {
		var infos = _FieldInfos;
		if (infos != null && infos.TryGetValue(name, out info)) {
			return true;
		}
		U.SkipInit(out info);
		return false;
	}

	public void SetFieldInfo(StringKey name, FieldInfo info) {
		var infos = _FieldInfos;
		if (infos == null) {
			// This becomes a conditional jump forward to not favor it
			goto Init;
		}

		var changes = _FieldInfoChanges;
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
		_FieldInfoChanges = changes = new();
		goto Set;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void DeleteFieldInfo(StringKey name)
		=> SetFieldInfo(name, new() { _IsDeleted = true });

	public void SetCachedFieldInfo(StringKey name, FieldInfo info) {
		var infos = _FieldInfos;
		if (infos == null) {
			// This becomes a conditional jump forward to not favor it
			goto Init;
		}

	Set:
		infos[name] = info;

		{
			var changes = _FieldInfoChanges;
			if (changes != null) {
				ref var infoRef = ref CollectionsMarshal.GetValueRefOrNullRef(changes, name);
				if (!U.IsNullRef(ref infoRef)) {
					infoRef = info;
				}
			}
		}

		return;

	Init:
		_FieldInfos = infos = new();
		goto Set;
	}

	public void UnloadFieldInfo(StringKey name) {
		var infos = _FieldInfos;
		if (infos != null) {
			infos.Remove(name);
			_FieldInfoChanges?.Remove(name);
		}
	}

	// --

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static long LoadRowId(KokoroCollection host, UniqueId uid)
		=> LoadRowId(host.Db, uid);

	internal static long LoadRowId(KokoroSqliteDb db, UniqueId uid)
		=> db.Cmd("SELECT rowid FROM Class WHERE uid=$uid")
			.AddParams(new("$uid", uid.ToByteArray()))
			.ConsumeScalar<long>();


	public void Load() {
		var db = Host.Db;
		using var cmd = db.Cmd("""
			SELECT uid,csum,ord,ifnull(grp,0)AS grp,name FROM Class
			WHERE rowid=$rowid
			""");
		cmd.Parameters.Add(new("$rowid", _RowId));

		using var r = cmd.ExecuteReader();
		if (r.Read()) {
			// Pending changes will be discarded
			_State = StateFlags.NoChanges;

			r.DAssert_Name(0, "uid");
			_Uid = r.GetUniqueId(0);

			r.DAssert_Name(1, "csum");
			_CachedCsum = r.GetBytesOrNull(1);

			r.DAssert_Name(2, "ord");
			_Ordinal = r.GetInt32(2);

			r.DAssert_Name(3, "grp");
			_GrpRowId = r.GetInt64(3);

			r.DAssert_Name(4, "name");
			_Name = r.GetString(4);

			return; // Early exit
		}

		// Otherwise, either deleted or never existed.
		Unload(); // Let that state materialize here then.
		_State = StateFlags.NotExists;
	}

	public void Load(bool core) {
		if (core) Load();
	}

	public void Load(bool core, StringKey fieldName1) {
		var db = Host.Db;
		using (new OptionalReadTransaction(db)) {
			if (core) Load();
			db.ReloadFieldNameCaches();
			InternalLoadFieldInfo(db, fieldName1);
		}
	}

	public void Load(bool core, StringKey fieldName1, StringKey fieldName2) {
		var db = Host.Db;
		using (new OptionalReadTransaction(db)) {
			if (core) Load();
			db.ReloadFieldNameCaches();
			InternalLoadFieldInfo(db, fieldName1);
			InternalLoadFieldInfo(db, fieldName2);
		}
	}

	public void Load(bool core, StringKey fieldName1, StringKey fieldName2, StringKey fieldName3) {
		var db = Host.Db;
		using (new OptionalReadTransaction(db)) {
			if (core) Load();
			db.ReloadFieldNameCaches();
			InternalLoadFieldInfo(db, fieldName1);
			InternalLoadFieldInfo(db, fieldName2);
			InternalLoadFieldInfo(db, fieldName3);
		}
	}

	public void Load(bool core, StringKey fieldName1, StringKey fieldName2, StringKey fieldName3, StringKey fieldName4) {
		var db = Host.Db;
		using (new OptionalReadTransaction(db)) {
			if (core) Load();
			db.ReloadFieldNameCaches();
			InternalLoadFieldInfo(db, fieldName1);
			InternalLoadFieldInfo(db, fieldName2);
			InternalLoadFieldInfo(db, fieldName3);
			InternalLoadFieldInfo(db, fieldName4);
		}
	}

	public void Load(bool core, StringKey fieldName1, StringKey fieldName2, StringKey fieldName3, StringKey fieldName4, StringKey fieldName5) {
		var db = Host.Db;
		using (new OptionalReadTransaction(db)) {
			if (core) Load();
			db.ReloadFieldNameCaches();
			InternalLoadFieldInfo(db, fieldName1);
			InternalLoadFieldInfo(db, fieldName2);
			InternalLoadFieldInfo(db, fieldName3);
			InternalLoadFieldInfo(db, fieldName4);
			InternalLoadFieldInfo(db, fieldName5);
		}
	}

	public void Load(bool core, StringKey fieldName1, StringKey fieldName2, StringKey fieldName3, StringKey fieldName4, StringKey fieldName5, StringKey fieldName6) {
		var db = Host.Db;
		using (new OptionalReadTransaction(db)) {
			if (core) Load();
			db.ReloadFieldNameCaches();
			InternalLoadFieldInfo(db, fieldName1);
			InternalLoadFieldInfo(db, fieldName2);
			InternalLoadFieldInfo(db, fieldName3);
			InternalLoadFieldInfo(db, fieldName4);
			InternalLoadFieldInfo(db, fieldName5);
			InternalLoadFieldInfo(db, fieldName6);
		}
	}

	public void Load(bool core, StringKey fieldName1, StringKey fieldName2, StringKey fieldName3, StringKey fieldName4, StringKey fieldName5, StringKey fieldName6, StringKey fieldName7) {
		var db = Host.Db;
		using (new OptionalReadTransaction(db)) {
			if (core) Load();
			db.ReloadFieldNameCaches();
			InternalLoadFieldInfo(db, fieldName1);
			InternalLoadFieldInfo(db, fieldName2);
			InternalLoadFieldInfo(db, fieldName3);
			InternalLoadFieldInfo(db, fieldName4);
			InternalLoadFieldInfo(db, fieldName5);
			InternalLoadFieldInfo(db, fieldName6);
			InternalLoadFieldInfo(db, fieldName7);
		}
	}

	public void Load(bool core, StringKey fieldName1, StringKey fieldName2, StringKey fieldName3, StringKey fieldName4, StringKey fieldName5, StringKey fieldName6, StringKey fieldName7, StringKey fieldName8) {
		var db = Host.Db;
		using (new OptionalReadTransaction(db)) {
			if (core) Load();
			db.ReloadFieldNameCaches();
			InternalLoadFieldInfo(db, fieldName1);
			InternalLoadFieldInfo(db, fieldName2);
			InternalLoadFieldInfo(db, fieldName3);
			InternalLoadFieldInfo(db, fieldName4);
			InternalLoadFieldInfo(db, fieldName5);
			InternalLoadFieldInfo(db, fieldName6);
			InternalLoadFieldInfo(db, fieldName7);
			InternalLoadFieldInfo(db, fieldName8);
			// TODO A counterpart that loads up to 15 field infos
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void Load(bool core, params StringKey[] fieldNames)
		=> Load(core, fieldNames.AsSpan());

	public void Load(bool core, ReadOnlySpan<StringKey> fieldNames) {
		var db = Host.Db;
		using (new OptionalReadTransaction(db)) {
			if (core) Load();
			db.ReloadFieldNameCaches();
			// TODO Unroll?
			foreach (var fieldName in fieldNames)
				InternalLoadFieldInfo(db, fieldName);
		}
	}

	public void Load(bool core, IEnumerable<StringKey> fieldNames) {
		var db = Host.Db;
		using (new OptionalReadTransaction(db)) {
			if (core) Load();
			db.ReloadFieldNameCaches();
			// TODO Unroll?
			foreach (var fieldName in fieldNames)
				InternalLoadFieldInfo(db, fieldName);
		}
	}

	[SkipLocalsInit]
	private void InternalLoadFieldInfo(KokoroSqliteDb db, StringKey name) {
		long fld = db.LoadStaleFieldId(name);
		if (fld == 0) goto NotFound;

		// Load field info
		using (var cmd = db.CreateCommand()) {
			cmd.Set("""
				SELECT ord,ifnull(sto,0)AS sto,ifnull(atarg,0)AS atarg FROM ClassToField
				WHERE cls=$cls AND fld=$fld
				""");
			var cmdParams = cmd.Parameters;
			cmdParams.Add(new("$cls", _RowId));
			cmdParams.Add(new("$fld", fld));

			using var r = cmd.ExecuteReader();
			if (r.Read()) {
				U.SkipInit(out FieldInfo info);

				r.DAssert_Name(0, "ord");
				info.Ordinal = r.GetInt32(0);

				r.DAssert_Name(1, "sto");
				info.StorageType = (FieldStorageType)r.GetInt32(1);
				info.StorageType.DAssert_Defined();

				r.DAssert_Name(2, "atarg");
				long atarg = r.GetInt64(2);
				info.AliasTarget = atarg == 0 ? null : db.LoadStaleFieldName(atarg);

				// Pending changes will be discarded
				_FieldInfoChanges?.Remove(name);

				SetCachedFieldInfo(name, info);

				return; // Early exit
			}
		}

	NotFound:
		// Otherwise, either deleted or never existed.
		// Let that state materialize here then.
		UnloadFieldInfo(name);
	}


	public void Unload() {
		UnloadCoreState();
		UnloadFieldInfos();
	}

	public void UnloadCoreState() {
		_State = default;
		_Uid = default;
		_Ordinal = default;
		_GrpRowId = default;
		_Name = default;
	}

	public void UnloadFieldInfos() {
		var infos = _FieldInfos;
		if (infos != null) {
			infos.Clear();
			_FieldInfoChanges = null;
		}
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
			rowid = db.Context.NextClassRowId();
			hasUsedNextRowId = true;
		} else {
			hasUsedNextRowId = false;
		}

		try {
			using var tx = new NestingWriteTransaction(db);
			// --
			// Save field infos
			{
				var fieldChanges = _FieldInfoChanges;
				if (fieldChanges != null)
					InternalSaveFieldInfos(db, fieldChanges, _RowId);
			}

			// --
			// Save core state

			using var cmd = db.Cmd(
				"INSERT INTO Class" +
				"(rowid,uid,csum,ord,grp,name)" +
				" VALUES" +
				$"($rowid,$uid,$csum,$ord,$grp,$name)");

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

			cmdParams.Add(new("$grp", RowIds.DBBox(_GrpRowId)));
			cmdParams.Add(new("$name", _Name.OrDBNull()));

			HashWithFieldInfos(db, rowid, ref hasher);
			Debug.Assert(2 == hasher_debug_i++);

			HashWithClassIncludes(db, rowid, ref hasher);
			Debug.Assert(3 == hasher_debug_i++);

			byte[] csum = FinishWithClassCsum(ref hasher);
			cmdParams.Add(new("$csum", csum));

			int updated = cmd.ExecuteNonQuery();
			Debug.Assert(updated == 1, $"Updated: {updated}");

			// COMMIT (or RELEASE) should be guaranteed to not fail at this
			// point if there's at least one operation that started a write.
			// - See, https://www.sqlite.org/rescode.html#busy
			tx.Commit();

			// Set new `csum` and clear pending changes (as they're now saved)
			{
				_CachedCsum = csum;
				_State = StateFlags.NoChanges;
				_FieldInfoChanges?.Clear();
			}
		} catch (Exception ex) when (hasUsedNextRowId && (
			ex is not SqliteException sqlex ||
			sqlex.SqliteExtendedErrorCode != SQLitePCL.raw.SQLITE_CONSTRAINT_ROWID
		)) {
			db.Context?.UndoClassRowId(rowid);
			throw;
		}

		_RowId = rowid;
		_Uid = uid;
	}

	[SkipLocalsInit]
	public void SaveChanges() {
		var state = _State;
		if (state < 0) goto Missing;
		if (state == StateFlags.NoChanges && _FieldInfoChanges == null) {
			goto Success;
		}

		var db = Host.Db; // Throws if host is already disposed
		using (var tx = new NestingWriteTransaction(db)) {
			// --
			// Save field infos
			{
				var fieldChanges = _FieldInfoChanges;
				if (fieldChanges != null)
					InternalSaveFieldInfos(db, fieldChanges, _RowId);
			}

			// --
			// Save core state

			using var cmd = db.CreateCommand();
			var cmdParams = cmd.Parameters;
			cmdParams.Add(new("$rowid", _RowId));

			StringBuilder cmdSb = new();
			cmdSb.Append("UPDATE Class SET\n");

			var hasher = Blake2b.CreateIncrementalHasher(ClassCsumDigestLength);
			// WARNING: The expected order of inputs to be fed to the above
			// hasher must be strictly as follows:
			//
			// 0. `uid`
			// 1. `ord`
			// 2. The 512-bit hash of, the list of `csum` data from `ClassToField`,
			// ordered by `ClassToField.ord,FieldName.name`
			// 3. The 512-bit hash of, the list of `uid` data from `Class ON Class.rowid=ClassToInclude.incl`,
			// ordered by `Class.uid`
			//
			// The resulting hash BLOB shall be prepended with a version varint.
			// Should any of the following happens, the version varint must
			// change:
			//
			// - The resulting hash BLOB length changes.
			// - The algorithm for the resulting hash BLOB changes.
			// - An input entry (from the list of inputs above) was removed.
			// - The order of an input entry (from the list of inputs above) was
			// changed or shifted.
			//
			// The version varint needs not to change if further input entries
			// were to be appended (from the list of inputs above).
			//
			int hasher_debug_i = 0; // Used only to help assert the above

			// TODO Avoid creating this when it won't be used at all
			using var cmd_old = db.Cmd("""
					SELECT uid,ord FROM Class
					WHERE rowid=$rowid
					""");
			cmd_old.Parameters.Add(new("$rowid", _RowId));
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

			if (state != StateFlags.NoChanges) {
				if ((state & StateFlags.Change_GrpRowId) != 0) {
					cmdSb.Append("grp=$grp,");
					cmdParams.Add(new("$grp", RowIds.DBBox(_GrpRowId)));
				}
				if ((state & StateFlags.Change_Name) != 0) {
					cmdSb.Append("name=$name,");
					cmdParams.Add(new("$name", _Name.OrDBNull()));
				}
			}

			HashWithFieldInfos(db, _RowId, ref hasher);
			Debug.Assert(2 == hasher_debug_i++);

			HashWithClassIncludes(db, _RowId, ref hasher);
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

			// Set new `csum` and clear pending changes (as they're now saved)
			{
				_CachedCsum = csum;
				_State = StateFlags.NoChanges;
				_FieldInfoChanges?.Clear();
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

	private static void HashWithFieldInfos(KokoroSqliteDb db, long clsRowId, ref Blake2bHashState hasher) {
		const int hasher_flds_dlen = 64; // 512-bit hash
		var hasher_flds = Blake2b.CreateIncrementalHasher(hasher_flds_dlen);

		using var cmd = db.Cmd("""
			SELECT cls2fld.csum AS csum FROM ClassToField AS cls2fld,FieldName AS fld
			WHERE cls2fld.cls=$cls AND fld.rowid=cls2fld.fld
			ORDER BY cls2fld.ord,fld.name
			""");
		var cmdParams = cmd.Parameters;
		cmdParams.Add(new("$cls", clsRowId));

		using var r = cmd.ExecuteReader();
		while (r.Read()) {
			r.DAssert_Name(0, "csum");
			// Will be empty span on null (i.e., won't throw NRE)
			var csum = (ReadOnlySpan<byte>)r.GetBytesOrNull(0);
			Debug.Assert(csum.Length > 0, $"Unexpected: field info has no `csum` (under class rowid {clsRowId})");
			hasher_flds.Update(csum);
		}

		hasher.Update(hasher_flds.FinishAndGet(stackalloc byte[hasher_flds_dlen]));
	}

	private static void HashWithClassIncludes(KokoroSqliteDb db, long clsRowId, ref Blake2bHashState hasher) {
		const int hasher_incls_dlen = 64; // 512-bit hash
		var hasher_incls = Blake2b.CreateIncrementalHasher(hasher_incls_dlen);

		using var cmd = db.Cmd("""
			SELECT cls.uid AS uid FROM ClassToInclude AS cls2incl,Class AS cls
			WHERE cls2incl.cls=$cls AND cls.rowid=cls2incl.incl
			ORDER BY uid
			""");
		var cmdParams = cmd.Parameters;
		cmdParams.Add(new("$cls", clsRowId));

		using var r = cmd.ExecuteReader();
		while (r.Read()) {
			r.DAssert_Name(0, "uid");
			UniqueId uid = r.GetUniqueId(0);
			hasher_incls.Update(uid.Span);
		}

		hasher.Update(hasher_incls.FinishAndGet(stackalloc byte[hasher_incls_dlen]));
	}

	[SkipLocalsInit]
	private static void InternalSaveFieldInfos(KokoroSqliteDb db, Dictionary<StringKey, FieldInfo> fieldInfoChanges, long clsRowId) {
		db.ReloadFieldNameCaches();

		using var cmd = db.CreateCommand();
		var cmdParams = cmd.Parameters;

		foreach (var (fieldName, info) in fieldInfoChanges) {
			long fld;
			if (!info._IsDeleted) {
				fld = db.LoadStaleOrEnsureFieldId(fieldName);
				goto UpdateFieldInfo;
			} else {
				fld = db.LoadStaleFieldId(fieldName);
				if (fld != 0) {
					goto DeleteFieldInfo;
				} else {
					// Deletion requested, but there's nothing to delete, as the
					// field is nonexistent.
					continue;
				}
			}

		UpdateFieldInfo:
			{
				var hasher_fld = Blake2b.CreateIncrementalHasher(FieldInfoCsumDigestLength);
				// WARNING: The expected order of inputs to be fed to the above
				// hasher must be strictly as follows:
				//
				// 0. `fieldName` in UTF8 with length prepended
				// 1. `info.Ordinal`
				// 2. `info.StorageType`
				// 3. `info.AliasTarget` in UTF8 with length prepended
				//
				// The resulting hash BLOB shall be prepended with a version
				// varint. Should any of the following happens, the version
				// varint must change:
				//
				// - The resulting hash BLOB length changes.
				// - The algorithm for the resulting hash BLOB changes.
				// - An input entry (from the list of inputs above) was removed.
				// - The order of an input entry (from the list of inputs above)
				// was changed or shifted.
				//
				// The version varint needs not to change if further input
				// entries were to be appended (from the list of inputs above).
				//
				int hasher_fld_debug_i = 0; // Used only to help assert the above

				hasher_fld.UpdateWithLELength(fieldName.Value.ToUTF8Bytes());
				Debug.Assert(0 == hasher_fld_debug_i++);

				cmd.Reset("""
					INSERT INTO ClassToField(cls,fld,csum,ord,sto,atarg)
					VALUES($cls,$fld,$csum,$ord,$sto,$atarg)
					ON CONFLICT DO UPDATE
					SET csum=$csum,ord=$ord,sto=$sto,atarg=$atarg
					""");
				cmdParams.Clear();
				cmdParams.Add(new("$cls", clsRowId));
				cmdParams.Add(new("$fld", fld));

				cmdParams.Add(new("$ord", info.Ordinal));
				hasher_fld.UpdateLE(info.Ordinal);
				Debug.Assert(1 == hasher_fld_debug_i++);

				var cmd_sto = cmdParams.Add(new() { ParameterName = "$sto" });
				var cmd_atarg = cmdParams.Add(new() { ParameterName = "$atarg" });

				var aliasTarget = info.AliasTarget;
				if (aliasTarget is null) {
					cmd_sto.Value = info.StorageType;
					Debug.Assert(typeof(FieldStorageTypeInt) == typeof(int));
					hasher_fld.UpdateLE((int)info.StorageType);
					Debug.Assert(2 == hasher_fld_debug_i++);

					cmd_atarg.Value = DBNull.Value;
					hasher_fld.UpdateLE(0); // Zero-length
					Debug.Assert(3 == hasher_fld_debug_i++);
				} else {
					cmd_sto.Value = DBNull.Value;
					Debug.Assert(typeof(FieldStorageTypeInt) == typeof(int));
					hasher_fld.UpdateLE((int)default(FieldStorageType));
					Debug.Assert(2 == hasher_fld_debug_i++);

					cmd_atarg.Value = db.LoadStaleOrEnsureFieldId(aliasTarget);
					hasher_fld.UpdateWithLELength(aliasTarget.Value.ToUTF8Bytes());
					Debug.Assert(3 == hasher_fld_debug_i++);
				}

				byte[] csum = FinishWithFieldInfoCsum(ref hasher_fld);
				cmdParams.Add(new("$csum", csum));

				int updated = cmd.ExecuteNonQuery();
				Debug.Assert(updated == 1, $"Updated: {updated}");

				continue;
			}

		DeleteFieldInfo:
			{
				cmd.Reset("DELETE FROM ClassToField WHERE (cls,fld)=($cls,$fld)");
				cmdParams.Clear();
				cmdParams.Add(new("$cls", clsRowId));
				cmdParams.Add(new("$fld", fld));

				int deleted = cmd.ExecuteNonQuery();
				// NOTE: It's possible for nothing to be deleted, for when the
				// field info didn't exist in the first place.
				Debug.Assert(deleted is 1 or 0);
			}
		}
		// Loop end
	}

	private const int ClassCsumDigestLength = 31; // 248-bit hash

	private static byte[] FinishWithClassCsum(ref Blake2bHashState hasher) {
		const int CsumVer = 1; // The version varint
		const int CsumVerLen = 1; // The varint length is a single byte for now
		Debug.Assert(VarInts.Length(CsumVer) == CsumVerLen);

		Span<byte> csum = stackalloc byte[CsumVerLen + ClassCsumDigestLength];
		csum[0] = CsumVer; // Prepend version varint
		hasher.Finish(csum[CsumVerLen..]);
		return csum.ToArray();
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
			newRowId = db.Context.NextClassRowId();
			hasUsedNextRowId = true;
		} else {
			hasUsedNextRowId = false;
		}

		int updated;
		try {
			updated = db.Cmd("UPDATE Class SET rowid=$newRowId WHERE rowid=$oldRowId")
				.AddParams(new("$oldRowId", oldRowId), new("$newRowId", newRowId))
				.Consume();
		} catch (Exception ex) when (hasUsedNextRowId && (
			ex is not SqliteException sqlex ||
			sqlex.SqliteExtendedErrorCode != SQLitePCL.raw.SQLITE_CONSTRAINT_ROWID
		)) {
			db.Context?.UndoClassRowId(newRowId);
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
		int deleted = db.Cmd("DELETE FROM Class WHERE rowid=$rowid")
			.AddParams(new("$rowid", rowid)).Consume();

		Debug.Assert(deleted is 1 or 0);
		return ((byte)deleted).ToUnsafeBool();
	}

	public static bool DeleteFrom(KokoroCollection host, UniqueId uid) {
		var db = host.Db;
		int deleted = db.Cmd("DELETE FROM Class WHERE uid=$uid")
			.AddParams(new("$uid", uid)).Consume();

		Debug.Assert(deleted is 1 or 0);
		return ((byte)deleted).ToUnsafeBool();
	}
}
