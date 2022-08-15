namespace Kokoro;
using Kokoro.Common.Sqlite;
using Kokoro.Common.Util;
using Kokoro.Internal;
using Kokoro.Internal.Sqlite;
using Microsoft.Data.Sqlite;

/// <summary>
/// A fielded entity that can be organized as a node in a tree-like structure.
/// </summary>
public sealed partial class Item : FieldedEntity {

	private long _RowId;
	private UniqueId _Uid;

	private long _ParentId;
	private int _Ordinal;
	private long _OrdModStamp;

	private long _DataModStamp;


	public Item(KokoroCollection host) : base(host) { }

	public Item(KokoroCollection host, long rowid)
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

	public long ParentId {
		get => _ParentId;
		set {
			_ParentId = value;
			_State |= StateFlags.Change_ParentId;
		}
	}

	public void SetCachedParentId(long parentId) => _ParentId = parentId;

	public int Ordinal {
		get => _Ordinal;
		set {
			_Ordinal = value;
			_State |= StateFlags.Change_Ordinal;
		}
	}

	public void SetCachedOrdinal(int ordinal) => _Ordinal = ordinal;

	public long OrdModStamp {
		get => _OrdModStamp;
		set {
			_OrdModStamp = value;
			_State |= StateFlags.Change_OrdModStamp;
		}
	}

	public void SetCachedOrdModStamp(long ordModStamp) => _OrdModStamp = ordModStamp;

	public new long SchemaId {
		get => _SchemaId;
		set {
			_SchemaId = value;
			_State |= StateFlags.Change_SchemaId;
		}
	}

	public long DataModStamp {
		get => _DataModStamp;
		set {
			_DataModStamp = value;
			_State |= StateFlags.Change_DataModStamp;
		}
	}

	public void SetCachedDataModStamp(long dataModStamp) => _DataModStamp = dataModStamp;


	internal sealed override Stream ReadHotStore(KokoroSqliteDb db) {
		return SqliteBlobSlim.Open(db,
			tableName: Prot.Item, columnName: "data", rowid: _RowId,
			canWrite: false, throwOnAccessFail: false) ?? Stream.Null;
	}

	internal sealed override Stream ReadColdStore(KokoroSqliteDb db) {
		return SqliteBlobSlim.Open(db,
			tableName: Prot.ItemToColdStore, columnName: "data", rowid: _RowId,
			canWrite: false, throwOnAccessFail: false) ?? Stream.Null;
	}

	// --

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static long LoadRowId(KokoroCollection host, UniqueId uid)
		=> LoadRowId(host.Db, uid);

	[SkipLocalsInit]
	internal static long LoadRowId(KokoroSqliteDb db, UniqueId uid) {
		using var cmd = db.CreateCommand();
		return cmd.Set($"SELECT rowid FROM {Prot.Item} WHERE uid=$uid")
			.AddParams(new("$uid", uid.ToByteArray()))
			.ExecScalarOrDefault<long>();
	}

	[SkipLocalsInit]
	internal void LoadSchemaId(KokoroSqliteDb db) {
		using var cmd = db.CreateCommand();
		cmd.Set($"SELECT schema FROM {Prot.Item} WHERE rowid=$rowid")
			.AddParams(new("$rowid", _RowId));

		using var r = cmd.ExecuteReader();
		if (r.Read()) {
			// Pending changes will be discarded
			_State &= ~StateFlags.Change_SchemaId;

			r.DAssert_Name(0, "schema");
			_SchemaId = r.GetInt64(0);

			return; // Early exit
		}

		// Otherwise, either deleted or never existed.
		Unload(); // Let that state materialize here then.
		_State = StateFlags.NotExists;
	}


	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void Load() => Load(Host.Db);

	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	[SkipLocalsInit]
	private void Load(KokoroSqliteDb db) {
		using var cmd = db.CreateCommand();
		cmd.Set(
			$"SELECT uid,ifnull(parent,0)AS parent,ord,ordModSt,schema,dataModSt FROM {Prot.Item}\n" +
			$"WHERE rowid=$rowid"
		).AddParams(new("$rowid", _RowId));

		using var r = cmd.ExecuteReader();
		if (r.Read()) {
			// Pending changes will be discarded
			_State &= StateFlags.Change_Classes;

			r.DAssert_Name(0, "uid");
			_Uid = r.GetUniqueId(0);

			r.DAssert_Name(1, "parent");
			_ParentId = r.GetInt64(1);

			r.DAssert_Name(2, "ord");
			_Ordinal = r.GetInt32(2);

			r.DAssert_Name(3, "ordModSt");
			_OrdModStamp = r.GetInt64(3);

			r.DAssert_Name(4, "schema");
			_SchemaId = r.GetInt64(4);

			r.DAssert_Name(5, "dataModSt");
			_DataModStamp = r.GetInt64(5);

			return; // Early exit
		}

		// Otherwise, either deleted or never existed.
		Unload(); // Let that state materialize here then.
		_State = StateFlags.NotExists;
	}


	public void Unload() {
		UnloadClassIds();
		UnloadFields();
		UnloadCore();
	}

	public void UnloadCore() {
		_State &= StateFlags.Change_Classes;
		_Uid = default;
		_ParentId = default;
		_Ordinal = default;
		_OrdModStamp = default;
		_SchemaId = default;
		_DataModStamp = default;
	}

	// --

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void SaveAsNew() => SaveAsNew(0);

	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	[SkipLocalsInit]
	public void SaveAsNew(long rowid) {
		var db = Host.Db; // Throws if host is already disposed

		bool hasUsedNextRowId;
		if (rowid == 0) {
			// Guaranteed not null if didn't throw above
			Debug.Assert(db.Context != null);
			rowid = db.Context.NextItemId();
			hasUsedNextRowId = true;
		} else {
			hasUsedNextRowId = false;
		}

		try {
			using var tx = new NestingWriteTransaction(db);

			// TODO Implement

			// Clear pending changes (as they're now saved)
			// --
			{
				_State = StateFlags.NoChanges;
				UnmarkFieldsAsChanged();
				UnmarkClassesAsChanged();
			}
		} catch (Exception ex) when (hasUsedNextRowId && (
			ex is not SqliteException sqlex ||
			sqlex.SqliteExtendedErrorCode != SQLitePCL.raw.SQLITE_CONSTRAINT_ROWID
		)) {
			db.Context?.UndoItemId(rowid);
			throw;
		}

		_RowId = rowid;
	}

	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	[SkipLocalsInit]
	public void SaveChanges() {
		var state = _State;

		if (state < 0) goto Missing;
		Debug.Assert((StateFlags)(-1) < 0, $"Underlying type of `{nameof(StateFlags)}` must be signed");

		bool mustRecompileFields;
		if (HasPendingFieldChanges) {
			mustRecompileFields = true;
		} else {
			if (state == StateFlags.NoChanges) goto Success;
			mustRecompileFields = (state & (StateFlags.Change_Classes|StateFlags.Change_SchemaId)) != 0;
		}

		var db = Host.Db; // Throws if host is already disposed
		using (var tx = new NestingWriteTransaction(db)) {
			using var cmd = db.CreateCommand();
			var cmdParams = cmd.Parameters;

			long rowid = _RowId;
			cmdParams.Add(new("$rowid", rowid));

			StringBuilder cmdSb = new();
			cmdSb.Append($"UPDATE {Prot.Item} SET\n");

			if ((state & (
				StateFlags.Change_ParentId|
				StateFlags.Change_Ordinal|
				StateFlags.Change_OrdModStamp
			)) != 0) {
				if ((state & StateFlags.Change_ParentId) != 0) {
					cmdParams.Add(new("$parent", RowIds.DBBox(_ParentId)));
					cmdSb.Append("parent=$parent,");
				}
				if ((state & StateFlags.Change_Ordinal) != 0) {
					cmdParams.Add(new("$ord", _Ordinal));
					cmdSb.Append("ord=$ord,");
				}

				long ordModStamp;
				if ((state & StateFlags.Change_OrdModStamp) == 0) {
					Debug.Assert((state & (
						StateFlags.Change_ParentId|
						StateFlags.Change_Ordinal)) != 0
					);
					ordModStamp = TimeUtils.UnixMillisNow();
					_OrdModStamp = ordModStamp;
				} else {
					ordModStamp = _OrdModStamp;
				}
				cmdParams.Add(new("$ordModSt", ordModStamp));
				cmdSb.Append("ordModSt=$ordModSt,");
			}

			if ((state & StateFlags.Change_Uid) != 0) {
				// This becomes a conditional jump forward to not favor it
				goto OnUidChanged;
			}

		CheckForOtherChanges:
			if (mustRecompileFields) {
				// TODO Implement
			} else {
				if ((state & StateFlags.Change_DataModStamp) != 0) {
					cmdParams.Add(new("$dataModSt", _DataModStamp));
					cmdSb.Append("dataModSt=$dataModSt");
				} else {
					Debug.Assert((state & ~StateFlags.NotExists) != 0, $"Must exist with at least 1 state changed");
					Debug.Assert(cmdParams.Count > 1, $"Needs at least 2 parameters set");
					Debug.Assert(cmdParams[0].ParameterName == "$rowid");

					Debug.Assert(cmdSb[^1] == ',');
					cmdSb.Length--; // Trim trailing comma
				}

				cmdSb.Append("WHERE rowid=$rowid");
				cmd.CommandText = cmdSb.ToString();

				int updated = cmd.ExecuteNonQuery();
				if (updated != 0) {
					Debug.Assert(updated == 1, $"Updated: {updated}");
					// COMMIT (or RELEASE) should be guaranteed to not fail at
					// this point if there's at least one operation that started
					// a write. See, https://www.sqlite.org/rescode.html#busy
					tx.Commit();
				} else {
					// This becomes a conditional jump forward to not favor it
					goto Missing_0;
				}
			}

			// Clear pending changes (as they're now saved)
			// --
			{
				_State = StateFlags.NoChanges;
				UnmarkFieldsAsChanged();
				UnmarkClassesAsChanged();
			}

			goto Success;

		Missing_0:
			goto Missing;

		OnUidChanged:
			{
				// NOTE: Changing the UID should be considerd similar to
				// removing an entry with the old UID, then recreating that
				// entry with a new UID, except that the modstamp isn't reset to
				// its initial value (which is zero) when the entry was created.
				cmdParams.Add(new("$uid", _Uid.ToByteArray()));
				cmdSb.Append("uid=$uid,");
				// TODO Create graveyard entry for the old UID to assist with syncing
				// - The modstamp of the graveyard entry should be equal to or
				// less than the item's data modstamp being saved in the current
				// operation.

				// Changes in UID are currently overseen by the data modstamp
				if ((state & StateFlags.Change_DataModStamp) == 0) {
					_DataModStamp = TimeUtils.UnixMillisNow();
					state |= StateFlags.Change_DataModStamp;
				}

				goto CheckForOtherChanges;
			}
		}

	Success:
		return;

	Missing:
		E_CannotUpdate_MRec(_RowId);

		[DoesNotReturn]
		static void E_CannotUpdate_MRec(long rowid) => throw new MissingRecordException(
			$"Cannot update `{nameof(Item)}` with rowid {rowid} as it's missing.");
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
			newRowId = db.Context.NextItemId();
			hasUsedNextRowId = true;
		} else {
			hasUsedNextRowId = false;
		}

		int updated;
		try {
			using var cmd = db.CreateCommand();
			updated = cmd.Set($"UPDATE {Prot.Item} SET rowid=$newRowId WHERE rowid=$oldRowId")
				.AddParams(new("$oldRowId", oldRowId), new("$newRowId", newRowId))
				.Exec();
		} catch (Exception ex) when (hasUsedNextRowId && (
			ex is not SqliteException sqlex ||
			sqlex.SqliteExtendedErrorCode != SQLitePCL.raw.SQLITE_CONSTRAINT_ROWID
		)) {
			db.Context?.UndoItemId(newRowId);
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
			$"Cannot update `{nameof(Item)}` with rowid {rowid} as it's missing.");
	}


	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool Delete() => DeleteFrom(Host, _RowId);

	public static bool DeleteFrom(KokoroCollection host, long rowid) {
		var db = host.Db;

		int deleted;
		using (var cmd = db.CreateCommand()) {
			deleted = cmd.Set($"DELETE FROM {Prot.Item} WHERE rowid=$rowid")
				.AddParams(new("$rowid", rowid)).Exec();

			// TODO Create graveyard entry for the deleted UID to assist with syncing
			// - Place in a write transaction, as necessary.
			// - Also provide a parameter to customize the graveyard entry's
			// modstamp.
			//   - Perhaps if nothing was deleted, and there's a custom modstamp
			//   given, look up for any existing graveyard entry and update its
			//   modstamp to the given custom modstamp.
		}

		Debug.Assert(deleted is 1 or 0);
		return ((byte)deleted).ToUnsafeBool();
	}

	public static bool DeleteFrom(KokoroCollection host, UniqueId uid) {
		var db = host.Db;

		int deleted;
		using (var cmd = db.CreateCommand()) {
			deleted = cmd.Set($"DELETE FROM {Prot.Item} WHERE uid=$uid")
				.AddParams(new("$uid", uid)).Exec();

			// TODO Create graveyard entry for the deleted UID to assist with syncing
			// - Place in a write transaction, as necessary.
			// - Also provide a parameter to customize the graveyard entry's
			// modstamp.
			//   - Perhaps if nothing was deleted, and there's a custom modstamp
			//   given, look up for any existing graveyard entry and update its
			//   modstamp to the given custom modstamp.
		}

		Debug.Assert(deleted is 1 or 0);
		return ((byte)deleted).ToUnsafeBool();
	}

	// --

	internal override string GetDebugLabel() => $"Item {_RowId}";
}
