namespace Kokoro.Internal.Caching;
using Kokoro.Common.Caching;

internal sealed class FieldNameToIdCache : LruCache<StringKey, long> {

	public FieldNameToIdCache(int maxSize) : base(maxSize) { }

	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	protected override unsafe int SizeOf(StringKey key, long value)
		=> sizeof(char) * key.Value.Length + StringKey.ApproxSizeOverhead + sizeof(long);
}
