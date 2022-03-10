namespace Kokoro.Test.SelfTest.Framework.Discovery;
using Kokoro.Test.Framework.Util;
using Moq;
using Xunit.Abstractions;
using Xunit.Sdk;

public class TestFrameworkDiscoverer_Facts {

	[Fact(DisplayName = $"{nameof(T001)} :: " +
		$"Error message is still correct about `{nameof(ITestFactAttribute)}` " +
		$"being currently implemented by both `{nameof(TestFactAttribute)}` " +
		$"and `{nameof(TestTheoryAttribute)}`")]
	public void T001() {
		using var scope = new AssertionCapture();

		var assemblyInfo = new ReflectionAssemblyInfo(typeof(TestFrameworkDiscoverer).Assembly);
		using var spyBus = new SpyMessageBus();
		using var testFrameworkDiscoverer = new TestFrameworkDiscoverer(
			assemblyInfo, new NullSourceInformationProvider(), new NullMessageSink());

		var mockRepo = new MockRepository(MockBehavior.Strict);
		var testMethod = mockRepo.OneOf<ITestMethod>(tm =>
			tm.Method.GetCustomAttributes(typeof(FactAttribute).AssemblyQualifiedName) == new[] {
				Mock.Of<IReflectionAttributeInfo>(r => r.Attribute == mockRepo.OneOf<FactAttribute>())
			} &&
			tm.Method.GetCustomAttributes(typeof(LabelAttribute).AssemblyQualifiedName) == new[] {
				Mock.Of<IReflectionAttributeInfo>(r => r.Attribute == mockRepo.OneOf<LabelAttribute>())
			} &&
			tm.TestClass.Class.Name == $"Sample_Test" &&
			tm.Method.Name == $"Test" &&
			tm.TestClass.TestCollection.TestAssembly.Assembly == assemblyInfo
		);

		testFrameworkDiscoverer.FindTestsForMethod(
			testMethod, includeSourceInformation: false,
			spyBus, TestFrameworkOptions.ForDiscovery()
		);
		var errorTestCase = (ExecutionErrorTestCase)spyBus.Messages
			.OfType<ITestCaseDiscoveryMessage>()
			.Select(msg => msg.TestCase)
			.Single();

		// Assert that the error message still says that `TestFactAttribute`
		// and `TestTheoryAttribute` both implements `ITestFactAttribute`
		errorTestCase.ErrorMessage.Should().MatchEquivalentOf(
			$"*whose [Fact]-derived attribute implements ?{nameof(ITestFactAttribute)}?" +
			$" (e.g., ?{nameof(TestFactAttribute)}? or ?{nameof(TestTheoryAttribute)}?)"
		);

		// Assert that the error message above is correct about `TestFactAttribute`
		// and `TestTheoryAttribute` both implementing `ITestFactAttribute`
		typeof(TestFactAttribute).Should().Implement<ITestFactAttribute>();
		typeof(TestTheoryAttribute).Should().Implement<ITestFactAttribute>();
	}

	// --

	// Exposes `protected` members that we want to test
	private class TestFrameworkDiscoverer : Test.Framework.Discovery.TestFrameworkDiscoverer {

		public TestFrameworkDiscoverer(
			IAssemblyInfo assemblyInfo,
			ISourceInformationProvider sourceProvider,
			IMessageSink diagnosticMessageSink,
			IXunitTestCollectionFactory? collectionFactory = null
		) : base(assemblyInfo, sourceProvider, diagnosticMessageSink, collectionFactory) { }

		public new bool FindTestsForMethod(ITestMethod testMethod, bool includeSourceInformation, IMessageBus messageBus, ITestFrameworkDiscoveryOptions discoveryOptions)
			=> base.FindTestsForMethod(testMethod, includeSourceInformation, messageBus, discoveryOptions);
	}
}
