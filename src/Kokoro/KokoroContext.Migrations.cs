namespace Kokoro;
using Kokoro.Common.IO;
using Microsoft.Data.Sqlite;

partial class KokoroContext {

	private static partial void InitMigrationMap(MigrationMap map) {
		map.Add(_ => _.Upgrade_v0w0_To_v0w1());
		map.Add(_ => _.Downgrade_v0w1_To_v0w0());
	}

	private void Upgrade_v0w0_To_v0w1() {
		using var db = new SqliteConnection(new SqliteConnectionStringBuilder() {
			DataSource = Path.Join(DataPath, "col.db"),
			Mode = SqliteOpenMode.ReadWriteCreate,
			Pooling = false,
			RecursiveTriggers = true,
		}.ToString());

		db.Open();

		const long SqliteDbAppId = 0x1c008087L; // Hint: It's an RGBA hex
		db.ExecuteNonQuery($"PRAGMA application_id={SqliteDbAppId}");
		db.ExecuteNonQuery($"PRAGMA journal_mode=WAL");
		db.ExecuteNonQuery($"PRAGMA synchronous=NORMAL");

		using var transaction = db.BeginTransaction();

		const string RowIdPk = "rowid INT PRIMARY KEY CHECK(rowid != 0)";
		const string UidUkCk = "uid BLOB UNIQUE CHECK(ifnull(length(uid) == 16, FALSE))";

		// NOTE: Even without `ON DELETE RESTRICT`, SQLite prohibits parent key
		// deletions -- consult the SQLite docs for details.
		const string OnRowIdFk = " ON UPDATE CASCADE";
		const string OnRowIdFkCascDel = " ON DELETE CASCADE" + " ON UPDATE CASCADE";
		const string OnRowIdFkNullDel = " ON DELETE SET NULL" + " ON UPDATE CASCADE";

		db.ExecuteNonQuery(

			// A string interning table for field names.
			"CREATE TABLE FieldNames(" +

				RowIdPk + "," +

				"name TEXT UNIQUE" +

			");" +

			// A schemable item.
			"CREATE TABLE Items(" +

				RowIdPk + "," +

				UidUkCk + "," +

				"parent INT REFERENCES Items" + OnRowIdFk + "," +

				// The item ordinal.
				"ordinal INT," +

				"schema INT REFERENCES Schemas" + OnRowIdFk + "," +

				// The blob comprising the list of modstamps and field data.
				//
				// The blob format is as follows, listed in order:
				// 1. A 64-bit integer stored as a varint.
				//   - In 64-bit form, the 3 LSBs indicate the minimum amount of
				//   bytes needed to store the largest integer in the list of
				//   integers that will be defined in *point 3*; the remaining
				//   bits indicate the number of integers in the said list.
				// 2. A 64-bit integer stored as a varint.
				//   - In 64-bit form, the 3 LSBs indicate the minimum amount of
				//   bytes needed to store the largest integer in the list of
				//   integers that will be defined in *point 4*; the remaining
				//   bits indicate the number of integers in the said list.
				// 3. The list of modstamps, as a list of integers.
				//   - Each is a span of milliseconds since Unix epoch.
				//   - Each occupies X bytes, where X is the minimum amount of
				//   bytes needed to store the largest integer in the list. The
				//   3 LSBs in *point 1* determines X: `0b000` (or `0x0`) means
				//   X is 1 byte, `0b111` (or `0x7`) means X is 8 bytes, etc.
				// 4. The list of field offsets, as a list of integers.
				//   - Each is a byte offset, where offset 0 is the location of
				//   the first byte in *point 5*.
				//   - Each occupies X bytes, where X is the minimum amount of
				//   bytes needed to store the largest integer in the list. The
				//   3 LSBs in *point 2* determines X: `0b000` (or `0x0`) means
				//   X is 1 byte, `0b111` (or `0x7`) means X is 8 bytes, etc.
				// 5. The list of field values -- the bytes simply concatenated.
				//
				// Quirks:
				// - A field offset may point to the same byte offset as another
				// if they share the same field value.
				// - During a modstamp lookup, if the modstamp index is greater
				// than the last modstamp index available, the largest modstamp
				// in the list of modstamps should be returned instead.
				//   - Lookups via negative modstamp indices should be
				//   considered an error.
				// - The first entry of the modstamp list is always the Unix
				// time when the `parent` and/or `ordinal` columns were last
				// modified.
				//   - The first time an item is created, both the `parent` and
				//   `ordinal` columns are considered modified for the first
				//   time. This implies that the modstamp list is never empty.
				// - If an item holds fat fields, the last entry of the modstamp
				// list is always the modstamp of the last modified fat field.
				"data BLOB" +

			");" +

			// -
			"CREATE TABLE ItemToColdFields(" +

				RowIdPk + " REFERENCES Items" + OnRowIdFkCascDel + "," +

				// The blob comprising the list of field offsets and field
				// values for cold fields.
				//
				// The blob format is similar to the `Items.data` column, except
				// that there's no modstamp list.
				//
				// The values for cold fields are initially stored in the parent
				// table, under the `Items.data` column, alongside hot fields.
				// However, when the `Items.data` column of an item row exceeds
				// a certain size, the values for the cold fields are moved here
				// (but their modstamps still remain in the parent table).
				//
				// Under normal conditions, this column is never empty, nor does
				// it contain an empty list of field offsets; nonetheless, field
				// values can still be empty as they can be zero-length blobs.
				"vals BLOB" +

			");" +

			// -
			"CREATE TABLE ItemToFatFields(" +

				"item INT REFERENCES Items" + OnRowIdFkCascDel + "," +

				"fld INT REFERENCES FieldNames" + OnRowIdFk + "," +

				// The field value.
				"val BLOB," +

				"PRIMARY KEY(item, fld)" +

			");" + // TODO Consider `WITHOUT ROWID` optimization?

			// An immutable data structure meant to describe a schemable -- a
			// schemable is anything where a schema can be attached/applied.
			"CREATE TABLE Schemas(" +

				RowIdPk + "," +

				// The cryptographic checksum of the schema's primary data,
				// which includes other tables that comprises the schema, but
				// excludes the `rowid` and modstamps.
				//
				// This is used as both a unique key and a lookup key to quickly
				// find an existing schema comprising the same data as another.
				// There should not be any schema whose primary data is the same
				// as another yet holds a different `rowid`.
				//
				// If null, the schema should be considered a draft, yet to be
				// finalized and not yet immutable.
				"usum BLOB UNIQUE," +

				// The expected number of modstamps in the schemable where the
				// schema is applied.
				"localModStampCount INT CHECK(ifnull(localModStampCount >= 0, FALSE))," +

				// The expected number of field data in the schemable where the
				// schema is applied.
				"localFieldCount INT CHECK(ifnull(localFieldCount >= 0, FALSE))," +

				// The blob comprising the list of shared modstamps and shared
				// field data.
				//
				// The blob format is the same as `Items.data` column with a few
				// differences:
				// - The modstamp list can be empty, and if so, the fallback for
				// modstamp lookups should simply be the Unix time when the
				// collection was "first" created (which is independent of
				// collection creation due to device syncs).
				"data BLOB" +

			");" +

			// -
			"CREATE TABLE SchemaToFields(" +

				"schema INT REFERENCES Schemas" + OnRowIdFkCascDel + "," +

				"fld INT REFERENCES FieldNames" + OnRowIdFk + "," +

				"index_st INT CHECK(ifnull(index_st >= 0, FALSE))," +

				"index_loc INT AS ((index_st >> 1) | (index_st & 0x1))," +

				// The field store type:
				// - 0b00: Shared
				// - 0b01: Hot
				// - 0b10: Cold
				// - 0b11: Fat
				"st INT AS (index_st & 0x3)," +

				// The field locality type:
				// - 0: Shared
				// - 1: Local
				"loc INT AS (st != 0)," +

				"modStampIndex INT CHECK(ifnull(modStampIndex >= 0, FALSE))," +

				"PRIMARY KEY(schema, fld)," +

				"UNIQUE(schema, index_loc)" +

			") WITHOUT ROWID;" +

			// Each schema is a snapshot of the schema types it used to build
			// itself. This table lists the schema types that was used.
			"CREATE TABLE SchemaToSchemaTypes(" +

				"schema INT REFERENCES Schemas" + OnRowIdFkCascDel + "," +

				"type INT REFERENCES SchemaTypes" + OnRowIdFk + "," +

				// The cryptographic checksum of the schema type when the schema
				// was created. Null if not available when the schema was
				// created, even though it might now be available at the present
				// moment -- remember, a schema is a snapshot.
				"csum BLOB," +

				// Quirks:
				// - Null when not contributing any shared fields.
				// - While a schema can never be modified upon creation, it can
				// inherit the modstamps of an older schema it was based on and
				// only differ on modstamp entries that really changed.
				"sharedModStampIndex INT CHECK(sharedModStampIndex >= 0)," +

				// Quirks:
				// - Null when not contributing any local fields.
				"localModStampIndex INT CHECK(localModStampIndex >= 0)," +

				"PRIMARY KEY(schema, type)," +

				"UNIQUE(schema, sharedModStampIndex)," +

				"UNIQUE(schema, localModStampIndex)" +

			") WITHOUT ROWID;" +

			// -
			"CREATE TABLE SchemaTypes(" +

				RowIdPk + "," +

				UidUkCk + "," +

				// The cryptographic checksum of the schema type's primary data,
				// which includes other tables that comprises the schema type,
				// but excludes the `rowid`, `src` and `name`.
				"csum BLOB," +

				// The schema type ordinal.
				"ordinal INT," +

				// TODO A trigger for when this column is nulled out: consider deleting the schema type as well
				"src INT REFERENCES Items" + OnRowIdFkNullDel + "," +

				"name TEXT COLLATE NOCASE," +

				"UNIQUE(src, name)" +

			");" +

			// -
			"CREATE TABLE SchemaTypeToFields(" +

				"type INT REFERENCES SchemaTypes" + OnRowIdFkCascDel + "," +

				"fld INT REFERENCES FieldNames" + OnRowIdFk + "," +

				// The field ordinal.
				"ordinal INT," +

				"PRIMARY KEY(type, fld)" +

			") WITHOUT ROWID;" +

			// -
			"CREATE TABLE SchemaTypeToIncludes(" +

				// The including schema type.
				"type INT REFERENCES SchemaTypes" + OnRowIdFkCascDel + "," +

				// The included schema type.
				"incl INT REFERENCES SchemaTypes" + OnRowIdFk + "," +

				"PRIMARY KEY(type, incl)" +

			") WITHOUT ROWID;" +

			// --
			""
		);

		// Done!
		transaction.Commit();
	}

	private void Downgrade_v0w1_To_v0w0() {
		FsUtils.DeleteDirectoryIfExists(Path.Join(DataPath, "ext"));
		FsUtils.DeleteDirectoryIfExists(Path.Join(DataPath, "media"));
		File.Delete(Path.Join(DataPath, "col.db"));
		File.Delete(Path.Join(DataPath, "conf.db"));
	}
}
