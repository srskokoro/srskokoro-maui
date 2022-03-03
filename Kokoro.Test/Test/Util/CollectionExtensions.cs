namespace Kokoro.Test.Util;

// From, https://github.com/xunit/xunit/blob/2.4.1/src/xunit.execution/Sdk/Utility/CollectionExtensions.cs
public static class CollectionExtensions {

	public static List<T> CastOrToList<T>(this IEnumerable<T> source) {
		return source as List<T> ?? source.ToList();
	}

	public static T[] CastOrToArray<T>(this IEnumerable<T> source) {
		return source as T[] ?? source.ToArray();
	}
}
