namespace Kokoro;
using Kokoro.Common.IO;
using static KokoroContext;

// This is a `ref struct` to ensure that it won't escape the current stack, so
// that only the current thread can only ever use this object.
//
// This is important because this object can perform operations that can
// corrupt the collection if accessed concurrently across multiple threads. By
// making this a `ref struct`, there won't be any room for misusage, since
// simply advising to avoid concurrent access doesn't necessarily mean that
// accidental misuse is impossible (with the consequences being too harmful to
// allow such accidents to happen).
//
// Another benefit is that, we will only have to mark the collection once for
// exclusive usage and multiple operations can be performed afterwards.
public readonly ref struct KokoroCollectionMaintenance {
	// Only really used when we need to access some collection entities.
	// Otherwise, it's merely used to take exclusive usage of the collection.
	private readonly KokoroCollectionExclusive _Collection;

	public KokoroCollectionMaintenance(KokoroContext context)
		=> _Collection = new KokoroCollectionExclusive(context);

	public void Dispose() => _Collection.Dispose();

	// --

	public void CleanUpTrash() {
		var context = _Collection._Context;
		var dataPath = context.DataPath;

		// Delete the rollback draft directory
		// -- possibly existing due to a previous process crash
		FsUtils.DeleteDirectoryIfExists($"{dataPath}{RollbackSuffix}{DraftSuffix}");

		// Delete the stale rollback directory
		// -- possibly existing due to a previous process crash
		FsUtils.DeleteDirectoryIfExists($"{dataPath}{RollbackSuffix}{StaleSuffix}");

		// Clean up and delete the trash directory
		FsUtils.DeleteDirectoryIfExists(context.GetTrash());
	}

	public class CompressRowIdsProgress : IProgressLite {
		private volatile string _TableBeingProcessed = "";
		private long _NumRowIdsInTheTable;
		private long _NumRowIdsProcessedSoFar;

		public string TableBeingProcessed {
			get => _TableBeingProcessed;
			set => _TableBeingProcessed = value;
		}

		public long NumRowIdsInTheTable {
			// NOTE: As per the reference source, `Volatile` guarantees atomicity for `long`
			get => Volatile.Read(ref _NumRowIdsInTheTable);
			set => Volatile.Write(ref _NumRowIdsInTheTable, value);
		}

		public long NumRowIdsProcessedSoFar {
			// NOTE: As per the reference source, `Volatile` guarantees atomicity for `long`
			get => Volatile.Read(ref _NumRowIdsProcessedSoFar);
			set => Volatile.Write(ref _NumRowIdsProcessedSoFar, value);
		}
	}

	/// <summary>
	/// Runs a long running operation that removes rowid gaps and ensures that
	/// the first rowid is 1.
	/// <para>
	/// Zero and negative rowids are left untouched, as they're reserved for
	/// special or internal use.
	/// </para>
	/// </summary>
	public void CompressRowIds(CompressRowIdsProgress progress) {
		// TODO Implement
		throw new NotImplementedException("TODO");
	}
}
