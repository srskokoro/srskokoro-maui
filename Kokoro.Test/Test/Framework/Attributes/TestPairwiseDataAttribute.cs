namespace Kokoro.Test.Framework.Attributes;
using System.Collections.Generic;
using System.Reflection;

/// <summary>
/// Provides a test method decorated with a <see cref="TheoryAttribute"/> with
/// arguments to run various combination of values for the parameters taken by
/// the test method using a pairwise strategy.
/// <para>
/// Unlike <see cref="PairwiseDataAttribute"/>, duplicates are also removed
/// from the returned data. This is useful for <see cref="Enum"/> types with
/// duplicate values.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public class TestPairwiseDataAttribute : PairwiseDataAttribute {

	public TestPairwiseDataAttribute() { }

	public override IEnumerable<object[]> GetData(MethodInfo testMethod) {
		return base.GetData(testMethod).Distinct(EnumerableEqualityComparer.ForArray<object>());
	}
}
