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
		const string UidUkCk = "uid BLOB UNIQUE NOT NULL CHECK(length(uid) = 16)";

		// NOTE: Even without `ON DELETE RESTRICT`, SQLite prohibits parent key
		// deletions -- consult the SQLite docs for details.
		const string OnRowIdFk = " ON UPDATE CASCADE";
		const string OnRowIdFkCascDel = " ON DELETE CASCADE" + " ON UPDATE CASCADE";
		const string OnRowIdFkNullDel = " ON DELETE SET NULL" + " ON UPDATE CASCADE";
		const string WithFkDfr = " DEFERRABLE INITIALLY DEFERRED";

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

		// An item, a node in a tree-like structure that can have fields, with
		// with its fields described by one or more classes, with the latter
		// also referred to as entity classes.
		//
		// An item is also said to be a type of fielded entity due to its
		// ability have fields, with the latter also referred to as entity
		// fields.
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
			// more other items or fielded entities.
			"schema INTEGER NOT NULL REFERENCES Schema" + OnRowIdFk + "," +

			// A modstamp, a number of milliseconds since Unix epoch, when the
			// `schema` column and/or any field data were last modified.
			//
			// This is also set to the "first" time the item is created, as the
			// item is considered modified for the first time. Note that, this
			// is independent of item creation due to device syncs, as syncing
			// should simply keep any existing modstamps on sync.
			//
			// If a plugin needs to have separate modstamp for a set of fields
			// (so as to have a separate sync conflict handling for it), place
			// the fields in a subrecord instead.
			// TODO-XXX Implement subrecord mechanics
			"data_modst INTEGER NOT NULL," +

			// The BLOB comprising the list of field data.
			//
			// The BLOB format is as follows, listed in order:
			// 1. A 32-bit unsigned integer stored as a varint.
			//   - In 32-bit form, the 2 LSBs indicate the minimum amount of
			//   bytes needed to store the largest integer in the list of
			//   integers that will be defined in *point 2*; the next bit (at
			//   bit index 2, of LSB 0 bit numbering) is reserved for a special
			//   purpose; the remaining bits indicate the number of integers in
			//   the list of integers mentioned earlier.
			// 2. The list of field offsets, as a list of 32-bit unsigned
			// integers.
			//   - Each is a byte offset, where offset 0 is the location of the
			//   first byte in *point 3*.
			//   - Each occupies X bytes, where X is the minimum amount of bytes
			//   needed to store the largest integer in the list. The 2 LSBs in
			//   *point 1* determines X: `0b00` (or `0x0`) means X is 1 byte,
			//   `0b11` (or `0x3`) means X is 4 bytes, etc.
			//   - Each field offset must always be greater than or equal to all
			//   preceding field offsets, so that the preceding field's length
			//   can be computed. Otherwise, the length cannot be computed and
			//   the preceding field will be assumed as having a length of zero.
			// 3. The list of field values -- the bytes simply concatenated.
			"data BLOB NOT NULL" +

		")");

		db.Exec("CREATE TABLE ItemToColdField(" +

			RowIdPk + " REFERENCES Item" + OnRowIdFkCascDel + WithFkDfr + "," +

			// The BLOB comprising the list of field offsets and field values
			// for cold fields.
			//
			// The BLOB format is similar to the `Item.data` column.
			//
			// The values for cold fields are initially stored in the parent
			// `Item` table, under the `data` column, alongside hot fields.
			// However, if the `data` column of an item row exceeds a certain
			// size, the values for the cold fields are moved here.
			//
			// Under normal circumstances, this column is never empty, nor does
			// it contain an empty list of field offsets; nonetheless, field
			// values can still be empty as they can be zero-length BLOBs.
			"data BLOB NOT NULL" +

		")");

		db.Exec("CREATE TABLE ItemToFloatingField(" +

			"item INTEGER NOT NULL REFERENCES Item" + OnRowIdFkCascDel + WithFkDfr + "," +

			"fld INTEGER NOT NULL REFERENCES FieldName" + OnRowIdFk + "," +

			// The field value bytes.
			"data BLOB NOT NULL," +

			"PRIMARY KEY(item, fld)" +

		")"); // TODO Consider `WITHOUT ROWID` optimization?"

		// A fielded entity's schema: an immutable data structure meant to
		// define how and where field data are stored.
		//
		// A schema may also be referred to as an entity schema.
		//
		// While an entity class describes a fielded entity's fields, a schema
		// is a compilation of that description, with the schema meant to
		// describe the "precise" layout and location of the fields in storage.
		//
		// A schema is also a compilation or compiled combination of zero or
		// more entity classes. That is, a schema is a frozen snapshot of the
		// various entity classes used to form it. Classes may be altered freely
		// at any time, but a schema ensures that those alterations doesn't
		// affect how an entity's fields are laid out or accessed, unless the
		// fielded entity is updated to use the newer state of its classes.
		db.Exec("CREATE TABLE Schema(" +

			RowIdPk + "," +

			// The cryptographic checksum of the schema's primary data, which
			// includes other tables that comprises the schema, but excludes the
			// `rowid` and `lfld_count`.
			//
			// This is used as both a unique key and a lookup key to quickly
			// find an existing schema comprising the same data as another.
			// There should not be any schema whose primary data is the same as
			// another yet holds a different `rowid`.
			//
			// If the first byte is `0x00` (zero), then the schema should be
			// considered a draft, yet to be finalized and not yet immutable.
			"usum BLOB NOT NULL UNIQUE," +

			// The expected number of field data in the fielded entity where the
			// schema is applied.
			//
			// This should always be equal to the number of local fields defined
			// by the schema -- see `SchemaToField` table.
			$"lfld_count INTEGER NOT NULL CHECK(lfld_count {BetweenInt32RangeGE0})," +

			// The BLOB comprising the list of field offsets and field values
			// for shared fields.
			//
			// The BLOB format is similar to the `Item.data` column.
			"data BLOB NOT NULL" +

		")");

		db.Exec("CREATE TABLE SchemaToField(" +

			"schema INTEGER NOT NULL REFERENCES Schema" + OnRowIdFkCascDel + WithFkDfr + "," +

			"fld INTEGER NOT NULL REFERENCES FieldName" + OnRowIdFk + "," +

			$"idx_a_sto INTEGER NOT NULL CHECK(idx_a_sto {BetweenInt32RangeGE0})," +

			"idx_loc INTEGER NOT NULL AS (idx << 1 | loc)," +

			// The field index.
			"idx INTEGER NOT NULL AS (idx_a_sto >> 3)," +

			// The field alias type:
			// - 0: Not an alias
			// - 1: Is an alias
			"a INTEGER NOT NULL AS ((idx_a_sto & 0x4) != 0)," +

			// The field store type:
			// - 0b00: Shared
			// - 0b01: Hot
			// - 0b10: Cold
			"sto INTEGER NOT NULL CHECK(sto BETWEEN 0x0 AND 0x2) AS (idx_a_sto & 0x3)," +

			// The field locality type:
			// - 0: Shared
			// - 1: Local
			"loc INTEGER NOT NULL AS (sto != 0)," +

			"PRIMARY KEY(schema, fld)" +

		") WITHOUT ROWID");

		db.Exec("CREATE UNIQUE INDEX [" +
			"UK_SchemaToField_C_idx_loc WHERE a=0" +
		"] ON " +
			"SchemaToField(idx_loc) WHERE a=0" +
		"");

		// An entity schema can also be thought of as an entity class set, in
		// that no data can enter a schema unless defined by an entity class: a
		// schema is composed by the various compiled states of its entity
		// classes, as each schema is a snapshot of the explicitly bound entity
		// classes used to assemble the schema.
		//
		// This table lists those "explicitly" bound entity classes.
		db.Exec("CREATE TABLE SchemaToDirectClass(" +

			"schema INTEGER NOT NULL REFERENCES Schema" + OnRowIdFkCascDel + WithFkDfr + "," +

			"cls INTEGER NOT NULL REFERENCES Class" + OnRowIdFk + "," +

			// The cryptographic checksum of the entity class when the schema
			// was created.
			"csum BLOB NOT NULL," +

			"PRIMARY KEY(schema, cls)" +

		") WITHOUT ROWID");

		// The implicitly bound entity classes used to assemble the schema. Such
		// "indirect" entity classes were "not" directly bound to the schema.
		// Otherwise, they shouldn't be in this table.
		//
		// This table is exactly like `SchemaToDirectClass` except that classes
		// not in that table goes into this table instead.
		//
		// Being able to distinguish between direct and indirect classes can be
		// useful when trying to copy the classes of one fielded entity to
		// another without having to copy all classes, as only the direct
		// classes are needed to be copied.
		db.Exec("CREATE TABLE SchemaToIndirectClass(" +

			"schema INTEGER NOT NULL REFERENCES Schema" + OnRowIdFkCascDel + WithFkDfr + "," +

			"cls INTEGER NOT NULL REFERENCES Class" + OnRowIdFk + "," +

			// The cryptographic checksum of the entity class when the schema
			// was created.
			"csum BLOB NOT NULL," +

			"PRIMARY KEY(schema, cls)" +

		") WITHOUT ROWID");

		// -
		db.Exec("CREATE TABLE Class(" +

			RowIdPk + "," +

			UidUkCk + "," +

			// The cryptographic checksum of the entity class's primary data,
			// which includes other tables that comprises the entity class, but
			// excludes the `rowid`, `grp`, `name`, and the contents of included
			// entity classes (only the included entity class's `uid` is used).
			//
			// TODO TRIGGER: If `csum` didn't change during an update, raise abort.
			"csum BLOB NOT NULL," +

			// The entity class ordinal.
			Ord_Int32Nn + "," +

			// The class group where the entity class belongs to.
			//
			// The class group is an item that hosts zero or more other classes.
			// It allows the class to be reachable and prevents the class from
			// being deleted should there be no schema referencing the class.
			//
			// TODO A trigger for when this column is nulled out: consider deleting the entity class as well
			"grp INTEGER REFERENCES Item" + OnRowIdFkNullDel + "," +

			// Quirks:
			// - Null when unnamed.
			"name TEXT," +

			"UNIQUE(grp, name)" +

		")");

		db.Exec("CREATE TABLE ClassToField(" +

			"cls INTEGER NOT NULL REFERENCES Class" + OnRowIdFkCascDel + WithFkDfr + "," +

			"fld INTEGER NOT NULL REFERENCES FieldName" + OnRowIdFk + "," +

			// The cryptographic checksum of the field definition's primary
			// data, which includes the `FieldName.Name` of this field and that
			// of the `atarg`, but excludes the `rowid` columns (i.e., `cls`,
			// `fld` and `atarg`).
			//
			// TODO TRIGGER: If `csum` didn't change during an update, raise abort.
			"csum BLOB NOT NULL," +

			// The field ordinal.
			Ord_Int32Nn + "," +

			// The field store type:
			// - 0b00: Shared
			// - 0b01: Hot
			// - 0b10: Cold
			// - NULL: Alias
			"sto INTEGER CHECK(sto BETWEEN 0x0 AND 0x2)," +

			// The field locality type:
			// - 0: Shared
			// - 1: Local
			// - NULL: Alias
			"loc INTEGER AS (sto != 0)," +

			// The field alias target.
			"atarg INTEGER REFERENCES FieldName" + OnRowIdFk + "," +

			// NOTE: Either `sto` or `atarg` is null, but not both, and neither
			// can both be nonnull, i.e., this is an "exclusive or" logic.
			"CHECK((sto ISNULL) IS NOT (atarg ISNULL))," +

			"PRIMARY KEY(cls, fld)" +

		") WITHOUT ROWID");

		db.Exec("CREATE TABLE ClassToInclude(" +

			// The including entity class.
			"cls INTEGER NOT NULL REFERENCES Class" + OnRowIdFkCascDel + WithFkDfr + "," +

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
