namespace Kokoro.Test.SelfTest.Framework.Discovery;
using Kokoro.Test.Framework.Discovery;
using Xunit.Sdk;

public class Discovery_Test {

	[Fact(DisplayName = $"{nameof(T001)} :: " +
		$"All `{nameof(IXunitTestCaseDiscoverer)}` implementations have " +
		$"proper `TypeName` constants")]
	public void T001() {
		using var scope = new AssertionScope();

		// Guard against name refactoring
		{
			const string expected = "TypeName";
			// Guard only against one name refactoring, since all the others
			// should be changed to match it also anyway.
			nameof(TestFactDiscoverer.TypeName).Should()
				.Be(expected, $"because it's currently being used in the test" +
				$" method `{nameof(FactAttribute.DisplayName)}` (since, " +
				$"`nameof({nameof(TestFactDiscoverer)}.{expected})` makes " +
				$"legibility bad)");
		}

		// --
		// Guard against namespace refactorings

		typeof(TestFactDiscoverer).ToString().Should()
			.Be(TestFactDiscoverer.TypeName);

		typeof(TestTheoryDiscoverer).ToString().Should()
			.Be(TestTheoryDiscoverer.TypeName);
	}
}
