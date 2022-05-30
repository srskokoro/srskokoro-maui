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

		const string Ordinal_Int32Nn = $"ordinal INTEGER NOT NULL CHECK(ordinal {BetweenInt32Range})";
		const string Ordinal_Int64Nn = $"ordinal INTEGER NOT NULL";

		// --

		// A string interning table for field names.
		db.Exec("CREATE TABLE FieldNames(" +

			RowIdPk + "," +

			"name TEXT UNIQUE NOT NULL" +

		")");

		// A table for interned field values.
		db.Exec("CREATE TABLE FieldValuesInterned(" +

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

		// An item, a node in a tree-like structure that can have classes and/or
		// fields, with the precise layout of its fields described by a schema.
		// An item is also said to be a type of a schemable entity.
		db.Exec("CREATE TABLE Items(" +

			RowIdPk + "," +

			UidUkCk + "," +

			"parent INTEGER REFERENCES Items" + OnRowIdFk + "," +

			// The item ordinal.
			Ordinal_Int32Nn + "," +

			// The number of milliseconds since Unix epoch, when the `parent`
			// and/or `ordinal` columns were last modified.
			//
			// Also, the first time an item is created, both the `parent` and
			// `ordinal` columns are considered modified for the first time.
			"ordModStamp INTEGER NOT NULL," +

			"schema INTEGER NOT NULL REFERENCES Schemas" + OnRowIdFk + "," +

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
			// lookups should simply be the `ordModStamp` (if available) or the
			// Unix time when the collection was "first" created (which is
			// independent of collection creation due to device syncs).
			// - If an item holds fat fields, the last entry of the modstamp
			// list is always the modstamp of the last modified fat field.
			"data BLOB NOT NULL" +

		")");

		db.Exec("CREATE TABLE ItemToColdFields(" +

			RowIdPk + " REFERENCES Items" + OnRowIdFkCascDel + "," +

			// The blob comprising the list of field offsets and field values
			// for cold fields.
			//
			// The blob format is similar to the `Items.data` column, except
			// that there's no modstamp list.
			//
			// The values for cold fields are initially stored in the parent
			// table, under the `Items.data` column, alongside hot fields.
			// However, when the `Items.data` column of an item row exceeds a
			// certain size, the values for the cold fields are moved here (but
			// their modstamps still remain in the parent table).
			//
			// Under normal circumstances, this column is never empty, nor does
			// it contain an empty list of field offsets; nonetheless, field
			// values can still be empty as they can be zero-length blobs.
			"vals BLOB NOT NULL" +

		")");

		db.Exec("CREATE TABLE ItemToFatFields(" +

			"item INTEGER NOT NULL REFERENCES Items" + OnRowIdFkCascDel + "," +

			"fld INTEGER NOT NULL REFERENCES FieldNames" + OnRowIdFk + "," +

			// The field value.
			"val BLOB NOT NULL," +

			"PRIMARY KEY(item, fld)" +

		")"); // TODO Consider `WITHOUT ROWID` optimization?"

		// An entity schema: an immutable data structure meant to describe an
		// entity, its classes, and its fields, along with any field that may be
		// shared by one or more other entities. Entities are also referred to
		// as schemable entities due to its relation with entity schemas.
		db.Exec("CREATE TABLE Schemas(" +

			RowIdPk + "," +

			// The cryptographic checksum of the schema's primary data, which
			// includes other tables that comprises the schema, but excludes the
			// `rowid`, `modStampCount` and `localFieldCount`.
			//
			// This is used as both a unique key and a lookup key to quickly
			// find an existing schema comprising the same data as another.
			// There should not be any schema whose primary data is the same as
			// another yet holds a different `rowid`.
			//
			// If null, the schema should be considered a draft, yet to be
			// finalized and not yet immutable.
			"usum BLOB UNIQUE," +

			// The expected number of modstamps in the entity where the schema
			// is applied.
			//
			// This should always be equal to the number of direct classes bound
			// to the schema -- see `SchemaToDirectClasses` table.
			$"modStampCount INTEGER NOT NULL CHECK(modStampCount {BetweenInt32RangeGE0})," +

			// The expected number of field data in the entity where the schema
			// is applied.
			//
			// This should always be equal to the number of local fields defined
			// by the schema -- see `SchemaToFields` table.
			$"localFieldCount INTEGER NOT NULL CHECK(localFieldCount {BetweenInt32RangeGE0})," +

			// The blob comprising the list of field offsets and field values
			// for shared fields.
			//
			// The blob format is similar to the `Items.data` column, except
			// that there's no modstamp list.
			"data BLOB NOT NULL" +

		")");

		db.Exec("CREATE TABLE SchemaToFields(" +

			"schema INTEGER NOT NULL REFERENCES Schemas" + OnRowIdFkCascDel + "," +

			"fld INTEGER NOT NULL REFERENCES FieldNames" + OnRowIdFk + "," +

			$"index_st INTEGER NOT NULL CHECK(index_st {BetweenInt32RangeGE0})," +

			"index_loc INTEGER NOT NULL AS ((index_st >> 1) | (index_st & 0x1))," +

			"index INTEGER NOT NULL AS (index_st >> 2)," +

			// The field store type:
			// - 0b00: Shared
			// - 0b01: Hot
			// - 0b10: Cold
			"st INTEGER NOT NULL CHECK(st BETWEEN 0x0 AND 0x2) AS (index_st & 0x3)," +

			// The field locality type:
			// - 0: Shared
			// - 1: Local
			"loc INTEGER NOT NULL AS (st != 0)," +

			// Quirks:
			// - Can also be used to lookup the direct class (in `SchemaToDirectClasses`
			// table) responsible for the field's inclusion.
			$"modStampIndex INTEGER NOT NULL CHECK(modStampIndex {BetweenInt32RangeGE0})," +

			// The entity class that defined this field, which can either be a
			// direct class (in `SchemaToDirectClasses`) or an indirect class
			// (in `SchemaToIndirectClasses`).
			"cls INTEGER NOT NULL REFERENCES EntityClasses" + OnRowIdFk + "," +

			"PRIMARY KEY(schema, fld)," +

			"UNIQUE(schema, index_loc)" +

		") WITHOUT ROWID");

		// An entity schema can also be thought of as an entity class set, in
		// that no data can enter a schema unless defined by an entity class: a
		// schema is composed by the various compiled states of its entity
		// classes. Each schema is a snapshot of the explicitly bound entity
		// classes used to assemble the schema.
		//
		// This table lists those "explicitly" bound entity classes.
		db.Exec("CREATE TABLE SchemaToDirectClasses(" +

			"schema INTEGER NOT NULL REFERENCES Schemas" + OnRowIdFkCascDel + "," +

			"cls INTEGER NOT NULL REFERENCES EntityClasses" + OnRowIdFk + "," +

			// The cryptographic checksum of the entity class when the schema
			// was created. Null if not available when the schema was created,
			// even though it might now be available at the present moment --
			// remember, an entity schema is a snapshot.
			"csum BLOB," +

			// Each direct entity class contributes a modstamp entry to the
			// schemable entity, initially set to the datetime the entity class
			// was directly attached (or reattached), and updated to the current
			// datetime whenever any of its fields (shared or local) are
			// updated. The modstamp entry is added even if the direct entity
			// class doesn't contribute any field. Note that, the fields that a
			// direct entity class contributes also include those from its
			// indirect entity classes.
			$"modStampIndex INTEGER NOT NULL CHECK(modStampIndex {BetweenInt32RangeGE0})," +

			"PRIMARY KEY(schema, cls)," +

			"UNIQUE(schema, modStampIndex)" +

		") WITHOUT ROWID");

		// The implicitly bound entity classes used to assemble the schema. Such
		// "indirect" entity classes were "not" directly bound to the schema.
		// Otherwise, they shouldn't be in this table.
		db.Exec("CREATE TABLE SchemaToIndirectClasses(" +

			"schema INTEGER NOT NULL REFERENCES Schemas" + OnRowIdFkCascDel + "," +

			"cls INTEGER NOT NULL REFERENCES EntityClasses" + OnRowIdFk + "," +

			// The cryptographic checksum of the entity class when the schema
			// was created. Null if not available when the schema was created,
			// even though it might now be available at the present moment --
			// remember, an entity schema is a snapshot.
			"csum BLOB," +

			// The direct class responsible for the implicit attachment of the
			// entity class to the schema. This is the explicitly bound entity
			// class that included the indirect entity class.
			"dcls INTEGER NOT NULL REFERENCES EntityClasses" + OnRowIdFk + "," +

			"PRIMARY KEY(schema, cls)" +

		") WITHOUT ROWID");

		// -
		db.Exec("CREATE TABLE EntityClasses(" +

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
			Ordinal_Int32Nn + "," +

			// TODO A trigger for when this column is nulled out: consider deleting the entity class as well
			"src INTEGER REFERENCES Items" + OnRowIdFkNullDel + "," +

			// Quirks:
			// - Null when unnamed.
			"name TEXT," +

			"UNIQUE(src, name)" +

		")");

		db.Exec("CREATE TABLE EntityClassToFields(" +

			"cls INTEGER NOT NULL REFERENCES EntityClasses" + OnRowIdFkCascDel + "," +

			"fld INTEGER NOT NULL REFERENCES FieldNames" + OnRowIdFk + "," +

			// The field ordinal.
			Ordinal_Int32Nn + "," +

			// The field store type:
			// - 0b00: Shared
			// - 0b01: Hot
			// - 0b10: Cold
			"st INTEGER NOT NULL CHECK(st BETWEEN 0x0 AND 0x2)," +

			// The field locality type:
			// - 0: Shared
			// - 1: Local
			"loc INTEGER NOT NULL AS (st != 0)," +

			"PRIMARY KEY(cls, fld)" +

		") WITHOUT ROWID");

		db.Exec("CREATE TABLE EntityClassToIncludes(" +

			// The including entity class.
			"cls INTEGER NOT NULL REFERENCES EntityClasses" + OnRowIdFkCascDel + "," +

			// The included entity class.
			"incl INTEGER NOT NULL REFERENCES EntityClasses" + OnRowIdFk + "," +

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
