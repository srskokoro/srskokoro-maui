namespace Kokoro.Test.Framework.Discovery;
using Xunit.Abstractions;
using Xunit.Sdk;

internal class TestFrameworkDiscoverer : XunitTestFrameworkDiscoverer {

	public TestFrameworkDiscoverer(
		IAssemblyInfo assemblyInfo,
		ISourceInformationProvider sourceProvider,
		IMessageSink diagnosticMessageSink,
		IXunitTestCollectionFactory? collectionFactory = null
	) : base(assemblyInfo, sourceProvider, diagnosticMessageSink, collectionFactory) { }

	protected override bool FindTestsForMethod(ITestMethod testMethod, bool includeSourceInformation, IMessageBus messageBus, ITestFrameworkDiscoveryOptions discoveryOptions) {
		var method = testMethod.Method;
		var labelAttributes = method.GetCustomAttributes(typeof(LabelAttribute)).FirstTwoOrDefault();

		if (labelAttributes.Item1 is not null) {
			string errorMessage;

			var factAttribute = method.GetCustomAttributes(typeof(FactAttribute)).FirstOrDefault();
			if (factAttribute is IReflectionAttributeInfo reflectFact
					&& reflectFact.Attribute is not ITestFactAttribute) {
				// Note that we don't fail when we don't have an `IReflectionAttributeInfo`,
				// since the actual attribute might indeed be fulfilling the contract.
				errorMessage = $"[Label]-derived attribute must only be " +
					$"given to a test method whose [Fact]-derived attribute " +
					$"implements `{nameof(ITestFactAttribute)}`";
			} else if (labelAttributes.Item2 is not null) {
				errorMessage = $"Test method `{testMethod.TestClass.Class.Name}." +
					$"{method.Name}` has multiple [Label]-derived attributes";
			} else {
				goto Success;
			}

			var testCase = new ExecutionErrorTestCase(
				DiagnosticMessageSink,
				discoveryOptions.MethodDisplayOrDefault(),
				discoveryOptions.MethodDisplayOptionsOrDefault(),
				testMethod, errorMessage
			);

			return ReportDiscoveredTestCase(testCase, includeSourceInformation, messageBus);
		}

	Success:
		return base.FindTestsForMethod(testMethod, includeSourceInformation, messageBus, discoveryOptions);
	}
}
