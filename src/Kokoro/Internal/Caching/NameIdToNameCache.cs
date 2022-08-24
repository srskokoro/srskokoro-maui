namespace Kokoro.Internal.Caching;
using Kokoro.Common.Caching;

internal sealed class NameIdToNameCache : LruCache<long, StringKey> {

	public NameIdToNameCache(int maxSize) : base(maxSize) { }

	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	protected override unsafe int SizeOf(long value, StringKey key) {
		return // --

			sizeof(char) * key.Value.Length +
			StringKey.ApproxSizeOverhead +

			sizeof(long);
	}
}
