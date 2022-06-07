namespace Kokoro;
using Kokoro.Common.IO;
using Microsoft.Data.Sqlite;

partial class KokoroContext {

	private static partial void InitMigrationMap(MigrationMap map) {
		map.Add(_ => _.Upgrade_v0w0_To_v0w1());
		map.Add(_ => _.Downgrade_v0w1_To_v0w0());
	}

	private void Upgrade_v0w0_To_v0w1() {
		// NOTE: Aside from ensuring that the temporary files used by SQLite are
		// kept together in one place, keeping a database file in its own
		// private subdirectory can also help avoid corruption in rare cases on
		// some filesystems.
		//
		// See,
		// - https://web.archive.org/web/20220317081844/https://www.sqlite.org/atomiccommit.html#_deleting_or_renaming_a_hot_journal:~:text=consider%20putting,subdirectory
		// - https://web.archive.org/web/20220407150600/https://www.sqlite.org/lockingv3.html#how_to_corrupt:~:text=could%20happen%2E-,The%20best%20defenses,themselves
		//
		string colDbDir = Path.Join(DataPath, "col.db");
		string colDbPath = Path.Join(colDbDir, "main");
		Directory.CreateDirectory(colDbDir);

		using var db = new SqliteConnection(new SqliteConnectionStringBuilder() {
			DataSource = colDbPath,
			Mode = SqliteOpenMode.ReadWriteCreate,
			Pooling = false,
			RecursiveTriggers = true,
		}.ToString());

		db.Open();

		// Hint: It's an RGBA hex
		const long SqliteDbAppId = 0x1c008087; // 469794951

		db.Exec($"PRAGMA application_id={SqliteDbAppId}");
		db.Exec($"PRAGMA journal_mode=WAL");
		db.Exec($"PRAGMA synchronous=NORMAL");
		db.Exec($"PRAGMA temp_store=FILE");

		using var transaction = db.BeginTransaction();

		const string RowIdPk = "rowid INTEGER PRIMARY KEY CHECK(rowid != 0)";
		const string UidUkCk = "uid BLOB UNIQUE NOT NULL CHECK(length(uid) == 16)";

		// NOTE: Even without `ON DELETE RESTRICT`, SQLite prohibits parent key
		// deletions -- consult the SQLite docs for details.
		const string OnRowIdFk = " ON UPDATE CASCADE";
		const string OnRowIdFkCascDel = " ON DELETE CASCADE" + " ON UPDATE CASCADE";
		const string OnRowIdFkNullDel = " ON DELETE SET NULL" + " ON UPDATE CASCADE";

		// --

		const string Int32MinHex = "-0x80000000";
		const string Int32MaxHex = "0x7FFFFFFF";

		const string BetweenInt32Range = $"BETWEEN {Int32MinHex} AND {Int32MaxHex}";
		const string BetweenInt32RangeGE0 = $"BETWEEN 0 AND {Int32MaxHex}";

		const string Ord_Int32Nn = $"ord INTEGER NOT NULL CHECK(ord {BetweenInt32Range})";
		const string Ord_Int64Nn = $"ord INTEGER NOT NULL";

		// --

		// A string interning table for field names.
		db.Exec("CREATE TABLE FieldName(" +

			RowIdPk + "," +

			"name TEXT UNIQUE NOT NULL" +

		")");

		// A table for interned field values.
		db.Exec("CREATE TABLE FieldValueInterned(" +

			RowIdPk + "," +

			// The number of fields referencing this record.
			//
			// Manually incremented/decremented due to the current nature of how
			// fields are stored. Unused records, and those whose reference
			// count is now zero, should be deleted when no longer needed.
			//
			// TODO A trigger for when this column is now zero: delete the row.
			"refCount INTEGER NOT NULL CHECK(refCount >= 0)," +

			// The field value bytes.
			"data BLOB UNIQUE NOT NULL" +

		")");

		// An item, a node in a tree-like structure that can have fields, with
		// with its fields described by one or more classes.
		//
		// An item is also said to be a type of classable entity due to its
		// ability be assigned classes, with the latter also referred to as
		// entity classes.
		db.Exec("CREATE TABLE Item(" +

			RowIdPk + "," +

			UidUkCk + "," +

			"parent INTEGER REFERENCES Item" + OnRowIdFk + "," +

			// The item ordinal.
			Ord_Int32Nn + "," +

			// A modstamp, a number of milliseconds since Unix epoch, when the
			// `parent` and/or `ord` columns were last modified.
			//
			// This is also set to the "first" time the item is created, as the
			// item is considered modified for the first time. Note that, this
			// is independent of item creation due to device syncs, as syncing
			// should simply keep any existing modstamps on sync.
			"ord_modst INTEGER NOT NULL," +

			// A compilation of an item's classes, which may be shared by one or
			// more other items or classable enities.
			"schema INTEGER NOT NULL REFERENCES Schema" + OnRowIdFk + "," +

			// The blob comprising the list of modstamps and field data.
			//
			// The blob format is as follows, listed in order:
			// 1. A 64-bit integer stored as a varint.
			//   - In 64-bit form, the 3 LSBs indicate the minimum amount of
			//   bytes needed to store the largest integer in the list of
			//   integers that will be defined in *point 3*; the remaining bits
			//   indicate the number of integers in the said list.
			// 2. A 64-bit integer stored as a varint.
			//   - In 64-bit form, the 3 LSBs indicate the minimum amount of
			//   bytes needed to store the largest integer in the list of
			//   integers that will be defined in *point 4*; the remaining bits
			//   indicate the number of integers in the said list.
			// 3. The list of field offsets, as a list of unsigned integers.
			//   - Each is a byte offset, where offset 0 is the location of the
			//   first byte in *point 5*.
			//   - Each occupies X bytes, where X is the minimum amount of bytes
			//   needed to store the largest integer in the list. The 3 LSBs in
			//   *point 1* determines X: `0b000` (or `0x0`) means X is 1 byte,
			//   `0b111` (or `0x7`) means X is 8 bytes, etc.
			// 4. The list of modstamps, as a list of unsigned integers.
			//   - Each is a span of milliseconds since Unix epoch.
			//   - Each occupies X bytes, where X is the minimum amount of bytes
			//   needed to store the largest integer in the list. The 3 LSBs in
			//   *point 2* determines X: `0b000` (or `0x0`) means X is 1 byte,
			//   `0b111` (or `0x7`) means X is 8 bytes, etc.
			// 5. The list of field values -- the bytes simply concatenated.
			//
			// Quirks:
			// - A field offset may point to the same byte offset as another if
			// they share the same field value.
			// - During a modstamp lookup, if the modstamp index is greater than
			// the last modstamp index available, the largest modstamp in the
			// list of modstamps should be returned instead.
			//   - Lookups via negative modstamp indices should be considered an
			//   error.
			// - If the modstamp list is empty, the fallback for modstamp
			// lookups should simply be the `ord_modst` (if available) or the
			// Unix time when the collection was "first" created (which is
			// independent of collection creation due to device syncs).
			// - If an item holds fat fields, the last entry of the modstamp
			// list is always the modstamp of the last modified fat field.
			"data BLOB NOT NULL" +

		")");

		db.Exec("CREATE TABLE ItemToColdField(" +

			RowIdPk + " REFERENCES Item" + OnRowIdFkCascDel + "," +

			// The blob comprising the list of field offsets and field values
			// for cold fields.
			//
			// The blob format is similar to the `Item.data` column, except
			// that there's no modstamp list.
			//
			// The values for cold fields are initially stored in the parent
			// table, under the `Item.data` column, alongside hot fields.
			// However, when the `Item.data` column of an item row exceeds a
			// certain size, the values for the cold fields are moved here (but
			// their modstamps still remain in the parent table).
			//
			// Under normal circumstances, this column is never empty, nor does
			// it contain an empty list of field offsets; nonetheless, field
			// values can still be empty as they can be zero-length blobs.
			"data BLOB NOT NULL" +

		")");

		db.Exec("CREATE TABLE ItemToFatField(" +

			"item INTEGER NOT NULL REFERENCES Item" + OnRowIdFkCascDel + "," +

			"fld INTEGER NOT NULL REFERENCES FieldName" + OnRowIdFk + "," +

			// The field value.
			"val BLOB NOT NULL," +

			"PRIMARY KEY(item, fld)" +

		")"); // TODO Consider `WITHOUT ROWID` optimization?"

		// A classable's schema: an immutable data structure meant to describe a
		// compilation or compiled combination of zero or more entity classes.
		//
		// While an entity class describes a classable entity's fields, a schema
		// is a compilation of that description, with the schema meant to
		// describe the "precise" layout of the fields in data storage.
		//
		// A schema is also a frozen snapshot of the various classes used to
		// form it. Classes may be altered freely at any time, but a schema
		// ensures that those alterations doesn't affect how an entity's fields
		// are laid out or accessed, unless the classable entity is updated to
		// use the newer state of its entity classes.
		//
		// A schema may also be referred to as an entity schema.
		db.Exec("CREATE TABLE Schema(" +

			RowIdPk + "," +

			// The cryptographic checksum of the schema's primary data, which
			// includes other tables that comprises the schema, but excludes the
			// `rowid`, `modst_count` and `lfld_count`.
			//
			// This is used as both a unique key and a lookup key to quickly
			// find an existing schema comprising the same data as another.
			// There should not be any schema whose primary data is the same as
			// another yet holds a different `rowid`.
			//
			// If the first byte is `0x00` (zero), then the schema should be
			// considered a draft, yet to be finalized and not yet immutable.
			"usum BLOB NOT NULL UNIQUE," +

			// The expected number of modstamps in the classable entity where
			// the schema is applied.
			//
			// This should always be equal to the number of direct classes bound
			// to the schema -- see `SchemaToDirectClass` table.
			$"modst_count INTEGER NOT NULL CHECK(modst_count {BetweenInt32RangeGE0})," +

			// The expected number of field data in the classable entity where
			// the schema is applied.
			//
			// This should always be equal to the number of local fields defined
			// by the schema -- see `SchemaToField` table.
			$"lfld_count INTEGER NOT NULL CHECK(lfld_count {BetweenInt32RangeGE0})," +

			// The blob comprising the list of field offsets and field values
			// for shared fields.
			//
			// The blob format is similar to the `Item.data` column, except that
			// there's no modstamp list.
			"data BLOB NOT NULL" +

		")");

		db.Exec("CREATE TABLE SchemaToField(" +

			"schema INTEGER NOT NULL REFERENCES Schema" + OnRowIdFkCascDel + "," +

			"fld INTEGER NOT NULL REFERENCES FieldName" + OnRowIdFk + "," +

			$"idx_sto INTEGER NOT NULL CHECK(idx_sto {BetweenInt32RangeGE0})," +

			"idx_loc INTEGER NOT NULL AS ((idx_sto >> 1) | (idx_sto & 0x1))," +

			// The field index.
			"idx INTEGER NOT NULL AS (idx_sto >> 2)," +

			// The field store type:
			// - 0b00: Shared
			// - 0b01: Hot
			// - 0b10: Cold
			"sto INTEGER NOT NULL CHECK(sto BETWEEN 0x0 AND 0x2) AS (idx_sto & 0x3)," +

			// The field locality type:
			// - 0: Shared
			// - 1: Local
			"loc INTEGER NOT NULL AS (sto != 0)," +

			// The modstamp index of this field in the classable entity where
			// the schema is applied (even if this field is a shared field).
			//
			// Quirks:
			// - Can also be used to lookup the direct class (in `SchemaToDirectClass`
			// table) responsible for the field's inclusion.
			$"modst_idx INTEGER NOT NULL CHECK(modst_idx {BetweenInt32RangeGE0})," +

			// The entity class that defined this field, which can either be a
			// direct class (in `SchemaToDirectClass`) or an indirect class (in
			// `SchemaToIndirectClass`).
			"cls INTEGER NOT NULL REFERENCES Class" + OnRowIdFk + "," +

			"PRIMARY KEY(schema, fld)," +

			"UNIQUE(schema, idx_loc)" +

		") WITHOUT ROWID");

		// A classable's schema can also be thought of as an entity class set,
		// in that no data can enter a schema unless defined by an entity class:
		// a schema is composed by the various compiled states of its entity
		// classes, as each schema is a snapshot of the explicitly bound entity
		// classes used to assemble the schema.
		//
		// This table lists those "explicitly" bound entity classes.
		db.Exec("CREATE TABLE SchemaToDirectClass(" +

			"schema INTEGER NOT NULL REFERENCES Schema" + OnRowIdFkCascDel + "," +

			"cls INTEGER NOT NULL REFERENCES Class" + OnRowIdFk + "," +

			// The cryptographic checksum of the entity class when the schema
			// was created. Null if not available when the schema was created,
			// even though it might now be available at the present moment --
			// remember, a schema is a snapshot.
			"csum BLOB," +

			// The modstamp index.
			//
			// Each direct entity class contributes a modstamp entry to the
			// classable entity, initially set to the datetime the entity class
			// was directly attached (or reattached), and updated to the current
			// datetime whenever any of the entity's fields (shared or local)
			// are updated. The modstamp entry is added even if the direct class
			// doesn't contribute any field. Note that, the fields that a direct
			// entity class contributes also include those from its indirect
			// entity classes.
			$"modst_idx INTEGER NOT NULL CHECK(modst_idx {BetweenInt32RangeGE0})," +

			"PRIMARY KEY(schema, cls)," +

			"UNIQUE(schema, modst_idx)" +

		") WITHOUT ROWID");

		// The implicitly bound entity classes used to assemble the schema. Such
		// "indirect" entity classes were "not" directly bound to the schema.
		// Otherwise, they shouldn't be in this table.
		db.Exec("CREATE TABLE SchemaToIndirectClass(" +

			"schema INTEGER NOT NULL REFERENCES Schema" + OnRowIdFkCascDel + "," +

			"cls INTEGER NOT NULL REFERENCES Class" + OnRowIdFk + "," +

			// The cryptographic checksum of the entity class when the schema
			// was created. Null if not available when the schema was created,
			// even though it might now be available at the present moment --
			// remember, a schema is a snapshot.
			"csum BLOB," +

			// The direct class responsible for the implicit attachment of the
			// entity class to the schema. This is the explicitly bound entity
			// class that included the indirect entity class.
			"dcls INTEGER NOT NULL REFERENCES Class" + OnRowIdFk + "," +

			"PRIMARY KEY(schema, cls)" +

		") WITHOUT ROWID");

		// -
		db.Exec("CREATE TABLE Class(" +

			RowIdPk + "," +

			UidUkCk + "," +

			// The cryptographic checksum of the entity class's primary data,
			// which includes other tables that comprises the entity class, but
			// excludes the `rowid`, `src`, `name`, and the contents of included
			// entity classes (only the included entity class's `uid` is used).
			//
			// Null if the entity class is runtime-bound, i.e., the runtime is
			// the one defining the entity class and the entity class definition
			// is never persisted to disk or DB.
			"csum BLOB," +

			// The entity class ordinal.
			Ord_Int32Nn + "," +

			// TODO A trigger for when this column is nulled out: consider deleting the entity class as well
			"src INTEGER REFERENCES Item" + OnRowIdFkNullDel + "," +

			// Quirks:
			// - Null when unnamed.
			"name TEXT," +

			"UNIQUE(src, name)" +

		")");

		db.Exec("CREATE TABLE ClassToField(" +

			"cls INTEGER NOT NULL REFERENCES Class" + OnRowIdFkCascDel + "," +

			"fld INTEGER NOT NULL REFERENCES FieldName" + OnRowIdFk + "," +

			// The field ordinal.
			Ord_Int32Nn + "," +

			// The field store type:
			// - 0b00: Shared
			// - 0b01: Hot
			// - 0b10: Cold
			"sto INTEGER NOT NULL CHECK(sto BETWEEN 0x0 AND 0x2)," +

			// The field locality type:
			// - 0: Shared
			// - 1: Local
			"loc INTEGER NOT NULL AS (sto != 0)," +

			"PRIMARY KEY(cls, fld)" +

		") WITHOUT ROWID");

		db.Exec("CREATE TABLE ClassToInclude(" +

			// The including entity class.
			"cls INTEGER NOT NULL REFERENCES Class" + OnRowIdFkCascDel + "," +

			// The included entity class.
			"incl INTEGER NOT NULL REFERENCES Class" + OnRowIdFk + "," +

			"PRIMARY KEY(cls, incl)" +

		") WITHOUT ROWID");

		// Done!
		transaction.Commit();
	}

	private void Downgrade_v0w1_To_v0w0() {
		FsUtils.DeleteDirectoryIfExists(Path.Join(DataPath, "ext"));
		FsUtils.DeleteDirectoryIfExists(Path.Join(DataPath, "media"));
		FsUtils.DeleteDirectoryIfExists(Path.Join(DataPath, "col.db"));
		FsUtils.DeleteDirectoryIfExists(Path.Join(DataPath, "conf.db"));
	}
}
