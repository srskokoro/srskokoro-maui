namespace Kokoro.Test.Util;

public static class ComprehensiveAssert {

	[Flags]
	public enum EqualityFlags {
		None = 0,
		CanBeEqualsNull,
	}

	public static void ProperlyImplements_IEquatable_Equals<T>(T testInstance, T equalInstance, T equalInstance2, T notEqualInstance, EqualityFlags flags = 0, bool notScopedInParent = false) where T : IEquatable<T> {
		/// NOTE: this method is mirrored by <see cref="ProperlyImplements_Equals"/> below.
		/// If you make any changes here, make sure to keep that version in sync as well.

		using var scope = new AssertionCapture();

		// NOTE: We're relying on the ability of `FluentAssertions` to
		// automatically provide context.

		testInstance.Equals(equalInstance).Should().BeTrue();
		testInstance.Equals(equalInstance2).Should().BeTrue();
		testInstance.Equals(notEqualInstance).Should().BeFalse();

		// Test: Reflexive Property
		{
			const string reasons = "`Equals()` must be reflexive";
			testInstance.Equals(testInstance).Should().BeTrue(because: reasons);
			equalInstance.Equals(equalInstance).Should().BeTrue(because: reasons);
		}

		// Test: Symmetric Property
		{
			const string reasons = "`Equals()` must be symmetric";
			equalInstance.Equals(testInstance).Should().BeTrue(because: reasons);
			notEqualInstance.Equals(testInstance).Should().BeFalse(because: reasons);
		}

		// Test: Transitive Property
		{
			const string reasons = "`Equals()` must be transitive";
			equalInstance.Equals(equalInstance2).Should().BeTrue(because: reasons);
			equalInstance.Equals(notEqualInstance).Should().BeFalse(because: reasons);
			equalInstance2.Equals(notEqualInstance).Should().BeFalse(because: reasons);
		}

		// Test: Consistency
		{
			const string reasonsForTrue = "`Equals()` must be consistent (it already returned `true` beforehand)";
			const string reasonsForFalse = "`Equals()` must be consistent (it already returned `false` beforehand)";

			testInstance.Equals(equalInstance).Should().BeTrue(because: reasonsForTrue);
			equalInstance.Equals(equalInstance2).Should().BeTrue(because: reasonsForTrue);
			notEqualInstance.Equals(equalInstance).Should().BeFalse(because: reasonsForFalse);
		}

		// Test: `null`
		if ((flags & EqualityFlags.CanBeEqualsNull) == 0) {
			const string reasons = "the object is nonnull and it supposedly shouldn't equal null";
			testInstance.Equals(null).Should().BeFalse(because: reasons);
			equalInstance.Equals(null).Should().BeFalse(because: reasons);
			equalInstance2.Equals(null).Should().BeFalse(because: reasons);
			notEqualInstance.Equals(null).Should().BeFalse(because: reasons);
		}

		if (notScopedInParent) {
			// Force end current scope
			scope.Strategy.ClearAndThrowIfAny();
		}
	}

	public static void ProperlyImplements_Equals(object testInstance, object equalInstance, object equalInstance2, object notEqualInstance, EqualityFlags flags = 0, bool notScopedInParent = false) {
		/// NOTE: this method is mirrored by <see cref="ProperlyImplements_IEquatable_Equals"/> above.
		/// If you make any changes here, make sure to keep that version in sync as well.

		using var scope = new AssertionCapture();

		// NOTE: We're relying on the ability of `FluentAssertions` to
		// automatically provide context.

		testInstance.Equals(equalInstance).Should().BeTrue();
		testInstance.Equals(equalInstance2).Should().BeTrue();
		testInstance.Equals(notEqualInstance).Should().BeFalse();

		// Test: Reflexive Property
		{
			const string reasons = "`Equals()` must be reflexive";
			testInstance.Equals(testInstance).Should().BeTrue(because: reasons);
			equalInstance.Equals(equalInstance).Should().BeTrue(because: reasons);
		}

		// Test: Symmetric Property
		{
			const string reasons = "`Equals()` must be symmetric";
			equalInstance.Equals(testInstance).Should().BeTrue(because: reasons);
			notEqualInstance.Equals(testInstance).Should().BeFalse(because: reasons);
		}

		// Test: Transitive Property
		{
			const string reasons = "`Equals()` must be transitive";
			equalInstance.Equals(equalInstance2).Should().BeTrue(because: reasons);
			equalInstance.Equals(notEqualInstance).Should().BeFalse(because: reasons);
			equalInstance2.Equals(notEqualInstance).Should().BeFalse(because: reasons);
		}

		// Test: Consistency
		{
			const string reasonsForTrue = "`Equals()` must be consistent (it already returned `true` beforehand)";
			const string reasonsForFalse = "`Equals()` must be consistent (it already returned `false` beforehand)";

			testInstance.Equals(equalInstance).Should().BeTrue(because: reasonsForTrue);
			equalInstance.Equals(equalInstance2).Should().BeTrue(because: reasonsForTrue);
			notEqualInstance.Equals(equalInstance).Should().BeFalse(because: reasonsForFalse);
		}

		// Test: `null`
		if ((flags & EqualityFlags.CanBeEqualsNull) == 0) {
			const string reasons = "the object is nonnull and it supposedly shouldn't equal null";
			testInstance.Equals(null).Should().BeFalse(because: reasons);
			equalInstance.Equals(null).Should().BeFalse(because: reasons);
			equalInstance2.Equals(null).Should().BeFalse(because: reasons);
			notEqualInstance.Equals(null).Should().BeFalse(because: reasons);
		}

		if (notScopedInParent) {
			// Force end current scope
			scope.Strategy.ClearAndThrowIfAny();
		}
	}

	public static void ProperlyImplements_GetHashCode(object testInstance, object equalInstance, object equalInstance2, bool notScopedInParent = false) {
		using var scope = new AssertionCapture();

		// NOTE: We're relying on the ability of `FluentAssertions` to
		// automatically provide context.

		int testHash = testInstance.GetHashCode();
		int equalHash = equalInstance.GetHashCode();
		int equalHash2 = equalInstance2.GetHashCode();

		{
			const string reasons = "equal instances must give the same hash code";
			(testHash == equalHash).Should().BeTrue(because: reasons);
			(testHash == equalHash2).Should().BeTrue(because: reasons);
		}

		// Test: Consistency
		{
			const string reasons = "`GetHashCode()` must be consistent (it must always return the same value)";
			(testHash == testInstance.GetHashCode()).Should().BeTrue(because: reasons);
			(equalHash == equalInstance.GetHashCode()).Should().BeTrue(because: reasons);
			(equalHash2 == equalInstance2.GetHashCode()).Should().BeTrue(because: reasons);
		}

		if (notScopedInParent) {
			// Force end current scope
			scope.Strategy.ClearAndThrowIfAny();
		}
	}

	public static void ProperlyImplements_IComparable<T>(T testInstance, T equalInstance, T lesserInstance, T greaterInstance, bool notScopedInParent = false) where T : IComparable {
		// TODO Implement
		throw new NotImplementedException("TODO");
	}

	public static void ProperlyImplements_IComparable_T<T>(T testInstance, T equalInstance, T lesserInstance, T greaterInstance, bool notScopedInParent = false) where T : IComparable<T> {
		// TODO Implement
		throw new NotImplementedException("TODO");
	}
}
