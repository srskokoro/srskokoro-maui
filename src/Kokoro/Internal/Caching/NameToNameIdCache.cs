namespace Kokoro.Internal.Caching;
using Kokoro.Common.Caching;

internal sealed class NameToNameIdCache : LruCache<StringKey, long> {

	public NameToNameIdCache(int maxSize) : base(maxSize) { }

	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	protected override unsafe int SizeOf(StringKey key, long value) {
		return // --

			sizeof(char) * key.Value.Length +
			StringKey.ApproxSizeOverhead +

			sizeof(long);
	}
}
