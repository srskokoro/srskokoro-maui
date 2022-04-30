namespace Kokoro.Test.Util;
using Xunit.Sdk;

public class ComprehensiveAssert_Facts {

	[TestFact]
	[TLabel($"[m!] is implemented properly")]
	public void T001_ProperlyImplements_IEquatable_Equals() {
		/// NOTE: this method is mirrored by <see cref="T002_ProperlyImplements_Equals"/> below.
		/// If you make any changes here, make sure to keep that version in sync as well.

		Guid guid1 = Guid.NewGuid();
		Guid guid2 = Guid.NewGuid();

		Skip.If(guid1 == guid2, "Rare event: 2 values returned from `Guid.NewGuid()` are apparently equal!");

		string str1 = guid1.ToString();
		string str2 = guid1.ToString();
		string str3 = guid1.ToString();
		string str4 = guid2.ToString();

		using (new AssertionCapture()) {
			// First, make sure our assumptions are correct
			str1.Should().NotBeSameAs(str2).And.NotBeSameAs(str3);
			str2.Should().NotBeSameAs(str3);
		}

		// --

		using var scope = new AssertionCapture();

		new Action(() => {
			ComprehensiveAssert.ProperlyImplements_IEquatable_Equals(str1, str2, str3, str4, notScopedInParent: true);
		}).Should().NotThrow();

		new Action(() => {
			ComprehensiveAssert.ProperlyImplements_IEquatable_Equals(1, 2, 3, 1, notScopedInParent: true);
		}).Should().Throw<XunitException>();
	}

	[TestFact]
	[TLabel($"[m!] is implemented properly")]
	public void T002_ProperlyImplements_Equals() {
		/// NOTE: this method is mirrored by <see cref="T001_ProperlyImplements_IEquatable_Equals"/> above.
		/// If you make any changes here, make sure to keep that version in sync as well.

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
			ComprehensiveAssert.ProperlyImplements_Equals(box1, box2, box3, box4, notScopedInParent: true);
		}).Should().NotThrow();

		new Action(() => {
			ComprehensiveAssert.ProperlyImplements_Equals(1, 2, 3, 1, notScopedInParent: true);
		}).Should().Throw<XunitException>();
	}

	[TestFact]
	[TLabel($"[m!] is implemented properly")]
	public void T003_ProperlyImplements_GetHashCode() {
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
			ComprehensiveAssert.ProperlyImplements_GetHashCode(box1, box2, box3, notScopedInParent: true);
		}).Should().NotThrow();

		new Action(() => {
			ComprehensiveAssert.ProperlyImplements_GetHashCode(1, 2, 3, notScopedInParent: true);
		}).Should().Throw<XunitException>();
	}
}
