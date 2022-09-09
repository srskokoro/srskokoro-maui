namespace Kokoro.Fixtures;
using Kokoro.Common.IO;

public abstract class KokoroCollectionFixtureBase : IDisposable {
	private const string CommonParentDir = "test_kcols";

	public readonly KokoroCollection Collection;

	public KokoroCollectionFixtureBase(
		ulong uidHighBits, ulong uidLowBits,
		bool ensureOperable = true
	) : this(
		new(highBits: uidHighBits, uidLowBits),
		ensureOperable: ensureOperable
	) { }

	public KokoroCollectionFixtureBase(UniqueId uid, bool ensureOperable = true) {
		var context = new KokoroContext(Path.Join(CommonParentDir, uid.ToFsString()), KokoroContextOpenMode.ReadWriteCreate);
		if (ensureOperable) {
			bool operable = context.TryUpgradeToVersion(KokoroDataVersion.LibVersion).Current.Operable;
			Debug.Assert(operable);
		}
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
