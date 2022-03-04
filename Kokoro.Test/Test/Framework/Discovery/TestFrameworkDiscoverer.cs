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

		var labelAttribute = method.GetCustomAttributes(typeof(LabelAttribute)).FirstOrDefault();
		if (labelAttribute is not null) {
			var factAttribute = method.GetCustomAttributes(typeof(FactAttribute)).FirstOrDefault();
			if (factAttribute is IReflectionAttributeInfo reflectFact
					&& reflectFact.Attribute is not ITestFactAttribute) {
				// Note that we don't fail when we don't have an `IReflectionAttributeInfo`,
				// since the actual attribute might indeed be fulfilling the contract.
				var errorMessage = $"[Label]-derived attribute must only be " +
					$"given to a test method whose [Fact]-derived attribute " +
					$"implements `{nameof(ITestFactAttribute)}`";

				var testCase = new ExecutionErrorTestCase(
					DiagnosticMessageSink,
					discoveryOptions.MethodDisplayOrDefault(),
					discoveryOptions.MethodDisplayOptionsOrDefault(),
					testMethod, errorMessage
				);

				return ReportDiscoveredTestCase(testCase, includeSourceInformation, messageBus);
			}
		}

		return base.FindTestsForMethod(testMethod, includeSourceInformation, messageBus, discoveryOptions);
	}
}
