namespace Kokoro;
using Kokoro.Common.Sqlite;
using Kokoro.Common.Util;
using Kokoro.Internal;
using Kokoro.Internal.Sqlite;
using Microsoft.Data.Sqlite;

partial class Item {

	public void Load(StringKey fieldName1) {
		var db = Host.Db;
		using (new OptionalReadTransaction(db)) {
			Load(db);
			if (Exists) {
				db.ReloadNameIdCaches();
				var fr = new FieldsReader(this, db);
				try {
					InternalLoadField(ref fr, fieldName1);
				} finally {
					fr.Dispose();
				}
			}
		}
	}

	public void Load(StringKey fieldName1, StringKey fieldName2) {
		var db = Host.Db;
		using (new OptionalReadTransaction(db)) {
			Load(db);
			if (Exists) {
				db.ReloadNameIdCaches();
				var fr = new FieldsReader(this, db);
				try {
					InternalLoadField(ref fr, fieldName1);
					InternalLoadField(ref fr, fieldName2);
				} finally {
					fr.Dispose();
				}
			}
		}
	}

	public void Load(StringKey fieldName1, StringKey fieldName2, StringKey fieldName3) {
		var db = Host.Db;
		using (new OptionalReadTransaction(db)) {
			Load(db);
			if (Exists) {
				db.ReloadNameIdCaches();
				var fr = new FieldsReader(this, db);
				try {
					InternalLoadField(ref fr, fieldName1);
					InternalLoadField(ref fr, fieldName2);
					InternalLoadField(ref fr, fieldName3);
				} finally {
					fr.Dispose();
				}
			}
		}
	}

	public void Load(StringKey fieldName1, StringKey fieldName2, StringKey fieldName3, StringKey fieldName4) {
		var db = Host.Db;
		using (new OptionalReadTransaction(db)) {
			Load(db);
			if (Exists) {
				db.ReloadNameIdCaches();
				var fr = new FieldsReader(this, db);
				try {
					InternalLoadField(ref fr, fieldName1);
					InternalLoadField(ref fr, fieldName2);
					InternalLoadField(ref fr, fieldName3);
					InternalLoadField(ref fr, fieldName4);
				} finally {
					fr.Dispose();
				}
			}
		}
	}

	public void Load(StringKey fieldName1, StringKey fieldName2, StringKey fieldName3, StringKey fieldName4, StringKey fieldName5) {
		var db = Host.Db;
		using (new OptionalReadTransaction(db)) {
			Load(db);
			if (Exists) {
				db.ReloadNameIdCaches();
				var fr = new FieldsReader(this, db);
				try {
					InternalLoadField(ref fr, fieldName1);
					InternalLoadField(ref fr, fieldName2);
					InternalLoadField(ref fr, fieldName3);
					InternalLoadField(ref fr, fieldName4);
					InternalLoadField(ref fr, fieldName5);
				} finally {
					fr.Dispose();
				}
			}
		}
	}

	public void Load(StringKey fieldName1, StringKey fieldName2, StringKey fieldName3, StringKey fieldName4, StringKey fieldName5, StringKey fieldName6) {
		var db = Host.Db;
		using (new OptionalReadTransaction(db)) {
			Load(db);
			if (Exists) {
				db.ReloadNameIdCaches();
				var fr = new FieldsReader(this, db);
				try {
					InternalLoadField(ref fr, fieldName1);
					InternalLoadField(ref fr, fieldName2);
					InternalLoadField(ref fr, fieldName3);
					InternalLoadField(ref fr, fieldName4);
					InternalLoadField(ref fr, fieldName5);
					InternalLoadField(ref fr, fieldName6);
				} finally {
					fr.Dispose();
				}
			}
		}
	}

	public void Load(StringKey fieldName1, StringKey fieldName2, StringKey fieldName3, StringKey fieldName4, StringKey fieldName5, StringKey fieldName6, StringKey fieldName7) {
		var db = Host.Db;
		using (new OptionalReadTransaction(db)) {
			Load(db);
			if (Exists) {
				db.ReloadNameIdCaches();
				var fr = new FieldsReader(this, db);
				try {
					InternalLoadField(ref fr, fieldName1);
					InternalLoadField(ref fr, fieldName2);
					InternalLoadField(ref fr, fieldName3);
					InternalLoadField(ref fr, fieldName4);
					InternalLoadField(ref fr, fieldName5);
					InternalLoadField(ref fr, fieldName6);
					InternalLoadField(ref fr, fieldName7);
				} finally {
					fr.Dispose();
				}
			}
		}
	}

	public void Load(StringKey fieldName1, StringKey fieldName2, StringKey fieldName3, StringKey fieldName4, StringKey fieldName5, StringKey fieldName6, StringKey fieldName7, StringKey fieldName8) {
		var db = Host.Db;
		using (new OptionalReadTransaction(db)) {
			Load(db);
			if (Exists) {
				db.ReloadNameIdCaches();
				var fr = new FieldsReader(this, db);
				try {
					InternalLoadField(ref fr, fieldName1);
					InternalLoadField(ref fr, fieldName2);
					InternalLoadField(ref fr, fieldName3);
					InternalLoadField(ref fr, fieldName4);
					InternalLoadField(ref fr, fieldName5);
					InternalLoadField(ref fr, fieldName6);
					InternalLoadField(ref fr, fieldName7);
					InternalLoadField(ref fr, fieldName8);
				} finally {
					fr.Dispose();
				}
			}
		}
		// TODO A counterpart that loads up to 16 fields
		// TODO Generate code via T4 text templates instead
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void Load(params StringKey[] fieldNames)
		=> Load(fieldNames.AsSpan());

	public void Load(ReadOnlySpan<StringKey> fieldNames) {
		var db = Host.Db;
		using (new OptionalReadTransaction(db)) {
			Load(db);
			if (Exists) {
				db.ReloadNameIdCaches();
				var fr = new FieldsReader(this, db);
				try {
					foreach (var fieldName in fieldNames)
						InternalLoadField(ref fr, fieldName);
				} finally {
					fr.Dispose();
				}
			}
		}
	}

	// --

	private protected sealed override FieldVal? OnLoadFloatingField(KokoroSqliteDb db, long fieldId) {
		Span<byte> encoded;

		using (var cmd = db.CreateCommand()) {
			cmd.Set(
				$"SELECT data\n" +
				$"FROM {Prot.ItemToFloatingField}\n" +
				$"WHERE (item,fld)=($item,$fld)"
			).AddParams(
				new("$item", _RowId),
				new("$fld", fieldId)
			);

			using var r = cmd.ExecuteReader();
			if (r.Read()) {
				r.DAssert_Name(0, "data");
				encoded = r.GetBytesOrEmpty(0);
				goto Found;
			} else {
				goto NotFound;
			}
		}

	Found:
		return DecodeFloatingFieldVal(encoded);

	NotFound:
		return null;
	}

	private protected sealed override FieldVal? OnSupplantFloatingField(KokoroSqliteDb db, long fieldId) {
		Span<byte> encoded;

		using (var cmd = db.CreateCommand()) {
			cmd.Set(
				$"DELETE FROM {Prot.ItemToFloatingField}\n" +
				$"WHERE (item,fld)=($item,$fld)\n" +
				$"RETURNING data"
			).AddParams(
				new("$item", _RowId),
				new("$fld", fieldId)
			);

			using var r = cmd.ExecuteReader();
			if (r.Read()) {
				r.DAssert_Name(0, "data");
				encoded = r.GetBytesOrEmpty(0);
				Debug.Assert(!r.Read(), $"Should've deleted only 1 floating field");
				goto Found;
			} else {
				goto NotFound;
			}
		}

	Found:
		return DecodeFloatingFieldVal(encoded);

	NotFound:
		return null;
	}

	private static FieldVal DecodeFloatingFieldVal(Span<byte> encoded) {
		int fValSpecLen = VarInts.Read(encoded, out ulong fValSpec);

		Debug.Assert(fValSpec <= FieldTypeHintInt.MaxValue);
		FieldTypeHint typeHint = (FieldTypeHint)fValSpec;

		if (typeHint != FieldTypeHint.Null) {
			byte[] data = encoded[fValSpecLen..].ToArray();
			return new(typeHint, data);
		}
		return FieldVal.Null;
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
	private static void InternalSaveFloatingFields(KokoroSqliteDb db, List<(long Id, FieldVal Entry)> changes, long itemId) {
		var changes_iter = changes.GetEnumerator();
		if (!changes_iter.MoveNext()) goto NoChanges;

		SqliteCommand?
			updCmd = null,
			delCmd = null;

		SqliteParameter
			cmd_item = new("$item", itemId),
			cmd_fld = new() { ParameterName = "$fld" };

		SqliteParameter
			updCmd_dataLength = null!;

		try {
		Loop:
			var (fld, fval) = changes_iter.Current;
			cmd_fld.Value = fld;

			if (fval.TypeHint != FieldTypeHint.Null) {
				if (updCmd != null) {
					goto UpdateFloatingField;
				} else {
					goto InitToUpdateFloatingField;
				}
			} else {
				goto ReadyToDeleteFloatingField;
			}

		UpdateFloatingField:
			{
				uint dataLength = fval.CountEncodeLength();
				Debug.Assert(dataLength > 0);
				updCmd_dataLength.Value = dataLength;

				try {
					int updated = updCmd.ExecuteNonQuery();
					Debug.Assert(updated == 1, $"Updated: {updated}");
				} catch (SqliteException ex) when (
					ex.SqliteErrorCode == SQLitePCL.raw.SQLITE_TOOBIG
				) {
					E_FloatingFieldDataTooLarge(db, dataLength);
					return;
				}

				using (var data = SqliteBlobSlim.Open(db,
					tableName: Prot.ItemToFloatingField, columnName: "data",
					rowid: itemId, canWrite: true, throwOnAccessFail: true
				)!) {
					fval.WriteTo(data);
				}

				goto Continue;
			}

		ReadyToDeleteFloatingField:
			if (delCmd != null) {
				goto DeleteFloatingField;
			} else {
				goto InitToDeleteFloatingField;
			}

		DeleteFloatingField:
			{
				int deleted = delCmd.ExecuteNonQuery();
				// NOTE: It's possible for nothing to be deleted, for when the
				// floating field didn't exist in the first place.
				Debug.Assert(deleted is 1 or 0, $"Deleted: {deleted}");

				goto Continue;
			}

		Continue:
			if (!changes_iter.MoveNext()) {
				goto Done;
			} else {
				// This becomes a conditional jump backward -- similar to a
				// `do…while` loop.
				goto Loop;
			}

		InitToUpdateFloatingField:
			{
				updCmd = db.CreateCommand();
				updCmd.Set(
					$"INSERT INTO {Prot.ItemToFloatingField}(item,fld,data)\n" +
					$"VALUES($item,$fld,zeroblob($dataLength))\n" +
					$"ON CONFLICT DO UPDATE\n" +
					$"SET data=zeroblob($dataLength)"
				).AddParams(
					cmd_item, cmd_fld,
					updCmd_dataLength = new() { ParameterName = "$dataLength" }
				);
				Debug.Assert(
					cmd_item.Value != null &&
					cmd_fld.Value != null
				);
				goto UpdateFloatingField;
			}

		InitToDeleteFloatingField:
			{
				delCmd = db.CreateCommand();
				delCmd.Set(
					$"DELETE FROM {Prot.ItemToFloatingField}\n" +
					$"WHERE (item,fld)=($item,$fld)"
				).AddParams(
					cmd_item, cmd_fld
				);
				Debug.Assert(
					cmd_item.Value != null &&
					cmd_fld.Value != null
				);
				goto DeleteFloatingField;
			}

		Done:
			;

		} finally {
			updCmd?.Dispose();
			delCmd?.Dispose();
		}

	NoChanges:
		;
	}
}
