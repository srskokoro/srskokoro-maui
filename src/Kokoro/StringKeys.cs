namespace Kokoro;

public static class StringKeys {

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static StringKey ToNewKey(this string value) => new(value);
}
