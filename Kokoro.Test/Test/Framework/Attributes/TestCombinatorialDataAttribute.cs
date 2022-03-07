namespace Kokoro.Test.Framework.Attributes;
using System.Collections.Generic;
using System.Reflection;

/// <summary>
/// Provides a test method decorated with a <see cref="TheoryAttribute"/> with
/// arguments to run every possible combination of values for the parameters
/// taken by the test method.
/// <para>
/// Unlike <see cref="CombinatorialDataAttribute"/>, duplicates are also
/// removed from the returned data. This is useful for <see cref="Enum"/> types
/// with duplicate values.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public class TestCombinatorialDataAttribute : CombinatorialDataAttribute {

	public TestCombinatorialDataAttribute() { }

	public override IEnumerable<object[]> GetData(MethodInfo testMethod) {
		return base.GetData(testMethod).Distinct(EnumerableEqualityComparer.ForArray<object>());
	}
}
