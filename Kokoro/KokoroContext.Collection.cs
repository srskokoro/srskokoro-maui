namespace Kokoro;

public partial class KokoroContext {

	private KokoroCollection? _Collection;

	public KokoroCollection Collection {
		get {
			var collection = _Collection;
			if (collection is null) {
				lock (_MigrationLock) {
					collection = _Collection;
					if (collection is null) {
						KokoroCollection.CheckIfOperable(this);
						_Collection = collection = CreateCollection();
					}
				}
			}
			return collection;
		}
	}

	protected virtual KokoroCollection CreateCollection() => new(this);

	public KokoroCollection ForceOperableCollection() {
		MigrateToOperableVersion();
		return Collection;
	}
}
