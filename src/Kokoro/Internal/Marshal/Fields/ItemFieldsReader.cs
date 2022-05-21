namespace Kokoro.Internal.Marshal.Fields;
using Microsoft.Data.Sqlite;
using System.IO;

internal sealed class ItemFieldsReader : AbsHotColdFieldsReader<Item> {

	public ItemFieldsReader(Item owner, Stream hotFieldsStream)
		: base(owner, hotFieldsStream) { }

	protected override FieldsReader CreateColdReader() {
		var item = Owner;
		var db = item.Host.Db;

		using var cmd = db.CreateCommand(
			"SELECT 1 FROM ItemToColdFields" +
			" WHERE rowid=$rowid");

		long rowid = item.RowId;
		cmd.Parameters.Add(new("$rowid", rowid));

		using var r = cmd.ExecuteReader();
		if (r.Read()) {
			// Same as `SqliteDataReader.GetStream()` but more performant
			SqliteBlob blob = new(db,
				tableName: "ItemToColdFields", columnName: "vals",
				rowid, readOnly: true);

			// NOTE: Even if both the `SqliteCommand` and `SqliteDataReader` are
			// disposed, the `SqliteBlob` stream will still be valid.
			return new ColdFieldsReader(item, blob);
		} else {
			return NullFieldsReader.Instance;
		}
	}
}
