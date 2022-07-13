namespace Kokoro.Test.Framework.Discovery;
using Xunit.Sdk;

public class Discovery_Facts {

	[Fact(DisplayName = $"{nameof(T001)} :: " +
		$"All `{nameof(IXunitTestCaseDiscoverer)}` implementations have " +
		$"proper `TypeName` constants")]
	public void T001() {
		using var scope = new AssertionCapture();

		// Guard against name refactoring
		{
			const string expected = "TypeName";
			// Guard only against one name refactoring, since all the others
			// should be changed to match it also anyway.
			nameof(TestFactDiscoverer.TypeName).Should()
				.Be(expected, $"because it's currently being used in the test" +
				$" method's `{nameof(FactAttribute.DisplayName)}` (since " +
				$"`nameof({nameof(TestFactDiscoverer)}.{expected})` makes " +
				$"legibility bad)");
		}

		// Guard against namespace refactorings
		// --

		typeof(TestFactDiscoverer).ToString().Should()
			.Be(TestFactDiscoverer.TypeName);

		typeof(TestTheoryDiscoverer).ToString().Should()
			.Be(TestTheoryDiscoverer.TypeName);

		typeof(TestDataDiscoverer).ToString().Should()
			.Be(TestDataDiscoverer.TypeName);
	}
}
