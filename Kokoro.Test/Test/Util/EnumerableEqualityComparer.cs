namespace Kokoro.Test.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

public static class EnumerableEqualityComparer {

	public static EnumerableEqualityComparer<TEnumerable, TElement> Create<TEnumerable, TElement>(IEqualityComparer<TElement>? comparer)
		where TEnumerable : class, IEnumerable<TElement> => new(comparer);

	public static EnumerableEqualityComparer<TEnumerable, TElement> Create<TEnumerable, TElement>()
		where TEnumerable : class, IEnumerable<TElement> => EnumerableEqualityComparer<TEnumerable, TElement>.Default;

	public static EnumerableEqualityComparer<IEnumerable<TElement>, TElement> Create<TElement>(IEqualityComparer<TElement>? comparer)
		=> new(comparer);

	public static EnumerableEqualityComparer<IEnumerable<TElement>, TElement> Create<TElement>()
		=> EnumerableEqualityComparer<IEnumerable<TElement>, TElement>.Default;

	public static EnumerableEqualityComparer<TElement[], TElement> ForArray<TElement>(IEqualityComparer<TElement>? comparer)
		=> new(comparer);

	public static EnumerableEqualityComparer<TElement[], TElement> ForArray<TElement>()
		=> EnumerableEqualityComparer<TElement[], TElement>.Default;
}

public class EnumerableEqualityComparer<TEnumerable, TElement>
	: IEqualityComparer<TEnumerable> where TEnumerable : class, IEnumerable<TElement> {

	public EnumerableEqualityComparer(IEqualityComparer<TElement>? comparer)
		=> Comparer = comparer;

	public readonly IEqualityComparer<TElement>? Comparer;

	public static EnumerableEqualityComparer<TEnumerable, TElement> Default
		=> DefaultImplementation.Instance;

	public bool Equals(TEnumerable? x, TEnumerable? y) {
		return x == y || (x is not null && y is not null && x.SequenceEqual(y, Comparer));
	}

	public int GetHashCode([DisallowNull] TEnumerable enumerables) {
		HashCode hashCode = default;
		foreach (var element in enumerables) {
			hashCode.Add(element, Comparer);
		}
		return hashCode.ToHashCode();
	}

	private class DefaultImplementation : EnumerableEqualityComparer<TEnumerable, TElement> {
		public static readonly DefaultImplementation Instance = new();
		public DefaultImplementation() : base(null) { }
	}
}
