namespace Kokoro.Internal.Caching;
using Kokoro.Common.Caching;

internal sealed class FieldIdToNameCache : LruCache<long, StringKey> {

	public FieldIdToNameCache(int maxSize) : base(maxSize) { }

	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	protected override unsafe int SizeOf(long value, StringKey key)
		=> sizeof(char) * key.Value.Length + StringKey.ApproxSizeOverhead + sizeof(long);
}
