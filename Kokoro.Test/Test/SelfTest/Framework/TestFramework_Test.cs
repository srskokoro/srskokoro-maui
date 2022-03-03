namespace Kokoro.Test.SelfTest.Framework;

using Kokoro.Test.Framework;

public class TestFramework_Test {

	[Fact(DisplayName = $"`{nameof(TestFramework)}` has proper `TypeName` constant")]
	public void T001() {
		using var scope = new AssertionScope();

		// --
		{
			const string expected = "TypeName";
			nameof(TestFramework.TypeName).Should()
				.Be(expected, $"because it's currently being used in the test" +
				$" method `{nameof(FactAttribute.DisplayName)}` (since, " +
				$"`nameof({nameof(TestFramework)}.{expected})` makes " +
				$"legibility bad)");
		}

		typeof(TestFramework).ToString().Should()
			.Be(TestFramework.TypeName);
	}
}
