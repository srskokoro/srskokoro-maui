namespace Kokoro.Test.SelfTest.Util;

using Xunit.Sdk;

public class ComprehensiveAssert_Facts {

	[TestFact]
	[TLabel($"[m] is implemented properly")]
	public void T001_ProperlyImplements_Equals() {
		Guid guid1 = Guid.NewGuid();
		Guid guid2 = Guid.NewGuid();

		Skip.If(guid1 == guid2, "Rare event: 2 values returned from `Guid.NewGuid()` are apparently equal!");

		object box1 = guid1;
		object box2 = guid1;
		object box3 = guid1;
		object box4 = guid2;

		using (new AssertionCapture()) {
			// First, make sure our assumptions are correct
			box1.Should().NotBeSameAs(box2).And.NotBeSameAs(box3);
			box2.Should().NotBeSameAs(box3);
		}

		// --

		using var scope = new AssertionCapture();

		new Action(() => {
			ComprehensiveAssert.ProperlyImplements_Equals(box1, box2, box3, box4);
		}).Should().NotThrow();

		new Action(() => {
			ComprehensiveAssert.ProperlyImplements_Equals<object>(1, 2, 3, 1);
		}).Should().Throw<XunitException>();
	}

	[TestFact]
	[TLabel($"[m] is implemented properly")]
	public void T002_ProperlyImplements_GetHashCode() {
		Guid guid1 = Guid.NewGuid();

		object box1 = guid1;
		object box2 = guid1;
		object box3 = guid1;

		using (new AssertionCapture()) {
			// First, make sure our assumptions are correct
			box1.Should().NotBeSameAs(box2).And.NotBeSameAs(box3);
			box2.Should().NotBeSameAs(box3);
		}

		// --

		using var scope = new AssertionCapture();

		new Action(() => {
			ComprehensiveAssert.ProperlyImplements_GetHashCode(box1, box2, box3);
		}).Should().NotThrow();

		new Action(() => {
			ComprehensiveAssert.ProperlyImplements_GetHashCode<object>(1, 2, 3);
		}).Should().Throw<XunitException>();
	}
}
