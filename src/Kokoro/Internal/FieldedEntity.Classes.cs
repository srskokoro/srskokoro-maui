namespace Kokoro.Internal;

partial class FieldedEntity {
	private Classes? _Classes;

	private sealed class Classes : HashSet<long> {
		internal ClassChanges? _Changes;
	}

	private sealed class ClassChanges : HashSet<long> { }
}
