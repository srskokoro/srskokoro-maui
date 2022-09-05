namespace Kokoro.Fixtures;
using Kokoro.Common.IO;

public abstract class KokoroCollectionFixtureBase : IDisposable {
	private const string CommonParentDir = "test_kcols";

	public readonly KokoroCollection Collection;

	public KokoroCollectionFixtureBase(ulong uidHighBits, ulong uidLowBits) : this(new(highBits: uidHighBits, uidLowBits)) { }

	public KokoroCollectionFixtureBase(UniqueId uid) {
		var context = new KokoroContext(Path.Join(CommonParentDir, uid.ToFsString()), KokoroContextOpenMode.ReadWriteCreate);
		Collection = new KokoroCollection(context);
	}

	public virtual void Dispose() {
		var collection = Collection;
		var context = collection.ContextOrNull;
		if (context != null) {
			collection.Dispose();

			string path = context.BasePath;
			context.Dispose();
			FsUtils.DeleteDirectoryIfExists(path);

			GC.SuppressFinalize(this);
		}
	}
}
