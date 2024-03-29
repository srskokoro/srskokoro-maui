﻿namespace Kokoro.Test.Framework;

public class TestFramework_Facts {

	[Fact(DisplayName = $"{nameof(T001)} :: " +
		$"`{nameof(TestFramework)}` has proper `TypeName` constant")]
	public void T001() {
		using var scope = new AssertionCapture();

		// Guard against name refactoring
		{
			const string expected = "TypeName";
			nameof(TestFramework.TypeName).Should()
				.Be(expected, $"because it's currently being used in the test" +
				$" method's `{nameof(FactAttribute.DisplayName)}` (since " +
				$"`nameof({nameof(TestFramework)}.{expected})` makes " +
				$"legibility bad)");
		}

		typeof(TestFramework).ToString().Should()
			.Be(TestFramework.TypeName);
	}
}
