namespace Kokoro.Test.Framework.Attributes;
using Kokoro.Test.Framework.Discovery;
using System.Collections.Generic;
using System.Reflection;
using Xunit.Sdk;

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
[DataDiscoverer(TestDataDiscoverer.TypeName, ThisAssembly.Name)]
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public class TestPairwiseDataAttribute : PairwiseDataAttribute {

	public TestPairwiseDataAttribute() { }

	/// <summary>
	/// Returns <c>true</c> if the data attribute wants to skip enumerating
	/// data during discovery. This will cause the theory to yield a single
	/// test case for all data, and the data discovery will be during test
	/// execution instead of discovery.
	/// </summary>
	public bool DisableDiscoveryEnumeration { get; set; }

	public override IEnumerable<object[]> GetData(MethodInfo testMethod) {
		return base.GetData(testMethod).Distinct(EnumerableEqualityComparer.ForArray<object>());
	}
}
