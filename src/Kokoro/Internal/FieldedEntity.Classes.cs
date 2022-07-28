namespace Kokoro.Internal;

partial class FieldedEntity {
	private Classes? _Classes;

	private sealed class Classes : HashSet<long> {
		internal AddedClasses? _Added;
	}

	private sealed class AddedClasses : HashSet<long> {
		internal RemovedClasses? _Removed;
	}

	private sealed class RemovedClasses : HashSet<long> {
	}
}
