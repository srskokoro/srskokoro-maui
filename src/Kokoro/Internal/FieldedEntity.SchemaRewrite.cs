namespace Kokoro.Internal;

partial class FieldedEntity {
	private const int MaxClassCount = byte.MaxValue + 1;

	private HashSet<long>? _AddedClasses;
	private HashSet<long>? _RemovedClasses;

	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	[SkipLocalsInit]
	private protected void RewriteSchema(ref FieldsReader fr, int hotStoreLimit, ref FieldsWriter fw) {
	}
}
