namespace Kokoro.DataProtocols;
using P = Prot_v0w1;

internal static class Setup_v0w1 {

	// Hint: It's an RGBA hex
	public const long SqliteAppId = 0x1c008087; // 469794951

	// --

	const string RowIdPk = $"rowid INTEGER PRIMARY KEY CHECK(rowid != 0)";

	// NOTE: Even without `ON DELETE RESTRICT`, SQLite prohibits parent key
	// deletions -- consult the SQLite docs for details.
	const string OnRowIdFk = "ON UPDATE CASCADE";
	const string OnRowIdFkCascDel = "ON DELETE CASCADE"+" "+"ON UPDATE CASCADE";
	const string OnRowIdFkNullDel = "ON DELETE SET NULL"+" "+"ON UPDATE CASCADE";
	const string WithFkDfr = "DEFERRABLE INITIALLY DEFERRED";


	const string UidUkCk = $"uid BLOB UNIQUE NOT NULL CHECK(length(uid) = 16)";

	const string Ord_Int32Nn = $"ord INTEGER NOT NULL CHECK(ord {BetweenInt32Range})";
	const string Ord_Int64Nn = $"ord INTEGER NOT NULL";

	// --

	const string Int32MinHex = "-0x80000000";
	const string Int32MaxHex = "0x7FFFFFFF";

	const string BetweenInt32Range = $"BETWEEN {Int32MinHex} AND {Int32MaxHex}";
	const string BetweenInt32RangeGE0 = $"BETWEEN 0 AND {Int32MaxHex}";

	const string IsBool = "IN (0,1)";

	// -=-

	// A string interning table for strings used as keys or key names.
	//
	// A string interning table that maps a string to an integer id, with the
	// string often used as a name or key to uniquely identify a given resource
	// under a certain context.
	public const string CreateTable_NameId =
		$"CREATE TABLE {P.NameId}(" +

			$"{RowIdPk}," +

			$"name TEXT UNIQUE NOT NULL" +

		$")";

	// An item, a node in a tree-like structure that can have fields, with its
	// fields described by one or more classes, with the latter also referred to
	// as entity classes.
	//
	// An item is also said to be a type of fielded entity due to its ability
	// have fields, with the latter also referred to as entity fields.
	public const string CreateTable_Item =
		$"CREATE TABLE {P.Item}(" +

			$"{RowIdPk}," +

			$"{UidUkCk}," +

			$"parent INTEGER REFERENCES {P.Item} {OnRowIdFk}," +

			// The item ordinal.
			$"{Ord_Int32Nn}," +

			// A modstamp, a number of milliseconds since Unix epoch, when the
			// `parent` and/or `ord` columns were last modified. This column's
			// primary use is to assist in sync conflict resolution.
			//
			// Given this column's primary use, the column is set to zero the
			// "first" time the item is created. This saves space as SQLite
			// encodes the zero value efficiently. Note that, this is
			// independent of item creation due to device syncs (hence, "first"
			// was emphasized), since syncing should simply keep any existing
			// modstamps on sync.
			//
			// If the value of this column is nonzero, it can be used to
			// determine when the item's `ord` and/or `parent` columns were last
			// modified. If this is important, it may be helpful to also store
			// the item's creation timestamp in a custom field data, which may
			// then be substituted for when this column is zero.
			$"ordModSt INTEGER NOT NULL," +

			// A compilation of an item's classes, which may be shared by one or
			// more other items or fielded entities.
			$"schema INTEGER NOT NULL REFERENCES {P.Schema} {OnRowIdFk}," +

			// A modstamp, a number of milliseconds since Unix epoch, when the
			// `schema` column and/or any field data were last modified. This
			// column's primary use is to assist in sync conflict resolution.
			//
			// Given this column's primary use, the column is set to zero the
			// "first" time the item is created. This saves space as SQLite
			// encodes the zero value efficiently. Note that, this is
			// independent of item creation due to device syncs (hence, "first"
			// was emphasized), since syncing should simply keep any existing
			// modstamps on sync.
			//
			// If a plugin needs to have separate modstamp for a set of fields
			// (so as to have a separate sync conflict handling for it), place
			// the fields in a subrecord instead.
			// TODO-XXX Implement subrecord mechanics
			//
			// If the value of this column is nonzero, it can be used to
			// determine when the item's fields and/or `schema` column were last
			// modified. If this is important, it may be helpful to also store
			// the item's creation timestamp in a custom field data, which may
			// then be substituted for when this column is zero.
			$"dataModSt INTEGER NOT NULL," +

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
			$"data BLOB NOT NULL" +

		$")";

	public const string CreateTable_ItemToColdStore =
		$"CREATE TABLE {P.ItemToColdStore}(" +

			$"{RowIdPk} REFERENCES {P.Item} {OnRowIdFkCascDel} {WithFkDfr}," +

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
			$"data BLOB NOT NULL" +

		$")";

	public const string CreateTable_ItemToFloatingField =
		$"CREATE TABLE {P.ItemToFloatingField}(" +

			$"item INTEGER NOT NULL REFERENCES {P.Item} {OnRowIdFkCascDel} {WithFkDfr}," +

			$"fld INTEGER NOT NULL REFERENCES {P.NameId} {OnRowIdFk}," +

			// The field value bytes.
			$"data BLOB NOT NULL," +

			$"PRIMARY KEY(item, fld)" +

		$")"; // TODO Consider `WITHOUT ROWID` optimization?"

	// -=-

	// A fielded entity's schema: an immutable data structure meant to define
	// how and where field data are stored.
	//
	// A schema may also be referred to as an entity schema.
	//
	// While an entity class describes a fielded entity's fields, a schema is a
	// compilation of that description, with the schema meant to describe the
	// "precise" layout and location of the fields in storage.
	//
	// A schema is also a compilation or compiled combination of zero or more
	// entity classes. That is, a schema is a frozen snapshot of the various
	// entity classes used to form it. Classes may be altered freely at any
	// time, but a schema ensures that those alterations doesn't affect how an
	// entity's fields are laid out or accessed, unless the fielded entity is
	// updated to use the newer state of its classes.
	public const string CreateTable_Schema =
		$"CREATE TABLE {P.Schema}(" +

			$"{RowIdPk}," +

			// The cryptographic checksum of the schema's primary data, which
			// includes other tables that comprises the schema, but excludes the
			// `rowid`, `localCount`, `hotCount` and `coldCount`.
			//
			// This is used as both a unique key and a lookup key to quickly
			// find an existing schema comprising the same data as another.
			// There should not be any schema whose primary data is the same as
			// another yet holds a different `rowid`.
			//
			// If the first byte is `0x00` (zero), then the schema should be
			// considered a draft, yet to be finalized and not yet immutable.
			$"usum BLOB NOT NULL UNIQUE," +

			// The maximum number of field data expected to be in the fielded
			// entity where the schema is applied.
			//
			// This should always be equal to the number of local fields defined
			// by the schema -- see `SchemaToField` table.
			$"localCount INTEGER NOT NULL CHECK(localCount {BetweenInt32RangeGE0}) AS (hotCount + coldCount)," +

			// The maximum number of hot field data expected to be in the
			// fielded entity where the schema is applied.
			//
			// This should always be equal to the number of hot fields defined
			// by the schema -- see `SchemaToField` table.
			$"hotCount INTEGER NOT NULL CHECK(hotCount {BetweenInt32RangeGE0})," +

			// The maximum number of cold field data expected to be in the
			// fielded entity where the schema is applied.
			//
			// This should always be equal to the number of cold fields defined
			// by the schema -- see `SchemaToField` table.
			$"coldCount INTEGER NOT NULL CHECK(coldCount {BetweenInt32RangeGE0})," +

			// The BLOB comprising the list of field offsets and field values
			// for shared fields.
			//
			// The BLOB format is similar to the `Item.data` column.
			$"data BLOB NOT NULL" +

		$")";

	public const string CreateTable_SchemaToField =
		$"CREATE TABLE {P.SchemaToField}(" +

			$"schema INTEGER NOT NULL REFERENCES {P.Schema} {OnRowIdFkCascDel} {WithFkDfr}," +

			$"fld INTEGER NOT NULL REFERENCES {P.NameId} {OnRowIdFk}," +

			$"idx_sto INTEGER NOT NULL CHECK(idx_sto {BetweenInt32RangeGE0})," +

			$"idx_loc INTEGER NOT NULL AS ((idx_sto >> 1) | (idx_sto & 0x1))," +

			// The field index.
			$"idx INTEGER NOT NULL AS (idx_sto >> 2)," +

			// The field store type:
			// - 0b00: Shared
			// - 0b01: Hot
			// - 0b10: Cold
			$"sto INTEGER NOT NULL CHECK(sto BETWEEN 0x0 AND 0x2) AS (idx_sto & 0x3)," +

			// The field locality type:
			// - 0: Shared
			// - 1: Local
			$"loc INTEGER NOT NULL AS (sto != 0)," +

			$"PRIMARY KEY(schema, fld)," +

			$"UNIQUE(schema, idx_loc)" +

		$") WITHOUT ROWID";

	// An entity schema can also be thought of as an entity class set, in that
	// no data can enter a schema unless defined by an entity class: a schema is
	// composed by the various compiled states of its entity classes, as each
	// schema is a snapshot of the explicitly bound entity classes used to
	// assemble the schema.
	public const string CreateTable_SchemaToClass =
		$"CREATE TABLE {P.SchemaToClass}(" +

			$"schema INTEGER NOT NULL REFERENCES {P.Schema} {OnRowIdFkCascDel} {WithFkDfr}," +

			$"cls INTEGER NOT NULL REFERENCES {P.Class} {OnRowIdFk}," +

			// The cryptographic checksum of the entity class when the schema
			// was created.
			$"csum BLOB NOT NULL," +

			// Whether the entity class was bound indirectly (TRUE) or directly
			// (FALSE).
			//
			// The indirect classes are the classes that were implicitly bound
			// because the directly bound classes included them -- see `ClassToInclude`
			// table.
			//
			// Being able to distinguish between direct and indirect classes can
			// be useful when trying to copy the classes of one fielded entity
			// to another without having to copy all classes, as only the direct
			// classes are needed to be copied.
			$"ind INTEGER NOT NULL CHECK(ind {IsBool})," +

			$"PRIMARY KEY(schema, cls)" +

		$") WITHOUT ROWID";

	public const string CreateIndex_IX_SchemaToClass_C_schema_C_ind_C_cls =
		$"CREATE INDEX [" +
			$"IX_{P.SchemaToClass}_C_schema_C_ind_C_cls" +
		$"] ON " +
			$"{P.SchemaToClass}(schema,ind,cls)" +
		$"";

	// -=-

	public const string CreateTable_Class =
		$"CREATE TABLE {P.Class}(" +

			$"{RowIdPk}," +

			$"{UidUkCk}," +

			// The cryptographic checksum of the entity class's primary data,
			// which includes other tables that comprises the entity class, but
			// excludes the `rowid`, `modst`, `grp`, `name`, and the contents of
			// included entity classes (only the included entity class's `uid`
			// is used).
			//
			// TODO TRIGGER: If `csum` didn't change during an update, raise abort.
			$"csum BLOB NOT NULL," +

			// A modstamp, a number of milliseconds since Unix epoch, when the
			// entity class or any associated data was last modified. Data
			// associated to an entity class includes the field infos (see
			// `ClassToField` table) and the list of included classes (see
			// `ClassToInclude` table) but excludes the data of the included
			// classes. This column's primary use is to assist in sync conflict
			// resolution.
			//
			// Given this column's primary use, the column is set to zero the
			// "first" time the class is created. This saves space as SQLite
			// encodes the zero value efficiently. Note that, this is
			// independent of class creation due to device syncs (hence, "first"
			// was emphasized), since syncing should simply keep any existing
			// modstamps on sync.
			$"modst INTEGER NOT NULL," +

			// The entity class ordinal.
			$"{Ord_Int32Nn}," +

			// The class group where the entity class belongs to.
			//
			// The class group is an item that hosts zero or more other classes.
			// It allows the class to be reachable and prevents the class from
			// being deleted should there be no schema referencing the class.
			//
			// TODO A trigger for when this column is nulled out: consider deleting the entity class as well
			$"grp INTEGER REFERENCES {P.Item} {OnRowIdFkNullDel}," +

			// Quirks:
			// - Null when unnamed.
			$"name INTEGER REFERENCES {P.NameId} {OnRowIdFk}," +

			$"UNIQUE(grp, name)" +

		$")";

	public const string CreateTable_ClassToField =
		$"CREATE TABLE {P.ClassToField}(" +

			$"cls INTEGER NOT NULL REFERENCES {P.Class} {OnRowIdFkCascDel} {WithFkDfr}," +

			$"fld INTEGER NOT NULL REFERENCES {P.NameId} {OnRowIdFk}," +

			// The cryptographic checksum of the field definition's primary
			// data, which includes the `NameId.name` of this field, but
			// excludes the `cls` and `fld` columns.
			//
			// TODO TRIGGER: If `csum` didn't change during an update, raise abort.
			$"csum BLOB NOT NULL," +

			// The field ordinal.
			$"{Ord_Int32Nn}," +

			// The field store type:
			// - 0b00: Shared
			// - 0b01: Hot
			// - 0b10: Cold
			$"sto INTEGER NOT NULL CHECK(sto BETWEEN 0x0 AND 0x2)," +

			// The field locality type:
			// - 0: Shared
			// - 1: Local
			$"loc INTEGER NOT NULL AS (sto != 0)," +

			$"PRIMARY KEY(cls, fld)" +

		$") WITHOUT ROWID";

	public const string CreateTable_ClassToInclude =
		$"CREATE TABLE {P.ClassToInclude}(" +

			// The including entity class.
			$"cls INTEGER NOT NULL REFERENCES {P.Class} {OnRowIdFkCascDel} {WithFkDfr}," +

			// The included entity class.
			$"incl INTEGER NOT NULL REFERENCES {P.Class} {OnRowIdFk}," +

			$"PRIMARY KEY(cls, incl)" +

		$") WITHOUT ROWID";
}
