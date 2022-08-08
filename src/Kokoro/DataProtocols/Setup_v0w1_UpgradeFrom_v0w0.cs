namespace Kokoro.DataProtocols;
using Kokoro.Common.Sqlite.Extensions;
using Microsoft.Data.Sqlite;
using Fs = Prot_v0w1.Fs;
using S = Setup_v0w1;

internal static class Setup_v0w1_UpgradeFrom_v0w0 {

	public static void DoMigrate(KokoroContext ctx) {
		string dataPath = ctx.DataPath;

		string colDbDir = Path.Join(dataPath, Fs.CollectionDbDir);
		Directory.CreateDirectory(colDbDir);

		string colDbPath = Path.Join(dataPath, Fs.CollectionDb);
		using var db = new SqliteConnection(new SqliteConnectionStringBuilder() {
			DataSource = colDbPath,
			Mode = SqliteOpenMode.ReadWriteCreate,
			Pooling = false,
			RecursiveTriggers = true,
		}.ToString());

		db.Open();

		// --

		db.Exec($"PRAGMA application_id={S.SqliteAppId}");
		db.Exec($"PRAGMA journal_mode=WAL");
		db.Exec($"PRAGMA synchronous=NORMAL");
		db.Exec($"PRAGMA temp_store=FILE");

		using var transaction = db.BeginTransaction();

		// -=-

		db.Exec(S.CreateTable_NameId);

		db.Exec(S.CreateTable_Item);
		db.Exec(S.CreateTable_ItemToColdStore);
		db.Exec(S.CreateTable_ItemToFloatingField);

		db.Exec(S.CreateTable_Schema);
		db.Exec(S.CreateTable_SchemaToField);
		db.Exec(S.CreateTable_SchemaToClass);
		db.Exec(S.CreateIndex_IX_SchemaToClass_C_schema_C_ind_C_cls);

		db.Exec(S.CreateTable_Class);
		db.Exec(S.CreateTable_ClassToField);
		db.Exec(S.CreateTable_ClassToInclude);

		// -=-

		// Done!
		transaction.Commit();
	}
}
