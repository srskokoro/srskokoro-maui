namespace Kokoro.Test.Util;

public static class CollectionUtils {

	public static (T?, T?) FirstTwoOrDefault<T>(this IEnumerable<T> source) {
		if (source is List<T> list) {
			int count = list.Count;
			if (count > 0) {
				if (count > 1) {
					return (list[0], list[1]);
				}
				return (list[0], default);
			}
			return default;
		} else {
			using var enumerator = source.Take(2).GetEnumerator();
			return (
				enumerator.MoveNext() ? enumerator.Current : default,
				enumerator.MoveNext() ? enumerator.Current : default
			);
		}
	}

	// From, https://github.com/xunit/xunit/blob/2.4.1/src/xunit.execution/Sdk/Utility/CollectionExtensions.cs
	public static List<T> CastOrToList<T>(this IEnumerable<T> source) {
		return source as List<T> ?? source.ToList();
	}

	// From, https://github.com/xunit/xunit/blob/2.4.1/src/xunit.execution/Sdk/Utility/CollectionExtensions.cs
	public static T[] CastOrToArray<T>(this IEnumerable<T> source) {
		return source as T[] ?? source.ToArray();
	}
}
