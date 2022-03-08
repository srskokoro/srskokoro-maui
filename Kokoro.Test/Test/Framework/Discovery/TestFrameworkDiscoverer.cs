namespace Kokoro.Test.Framework.Discovery;
using Xunit.Abstractions;
using Xunit.Sdk;

public class TestFrameworkDiscoverer : XunitTestFrameworkDiscoverer {

	public TestFrameworkDiscoverer(
		IAssemblyInfo assemblyInfo,
		ISourceInformationProvider sourceProvider,
		IMessageSink diagnosticMessageSink,
		IXunitTestCollectionFactory? collectionFactory = null
	) : base(assemblyInfo, sourceProvider, diagnosticMessageSink, collectionFactory) { }

	[ThreadStatic]
	private static Dictionary<int, IMethodInfo>? _CurrentTestNumbers = null;

	protected override bool FindTestsForType(ITestClass testClass, bool includeSourceInformation, IMessageBus messageBus, ITestFrameworkDiscoveryOptions discoveryOptions) {
		try {
			_CurrentTestNumbers = new();
			return base.FindTestsForType(testClass, includeSourceInformation, messageBus, discoveryOptions);
		} finally {
			_CurrentTestNumbers = null;
		}
	}

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
					$"implements `{nameof(ITestFactAttribute)}` (e.g., " +
					$"`{nameof(TestFactAttribute)}` or `{nameof(TestTheoryAttribute)}`)";
			} else if (labelAttributes.Item2 is not null) {
				errorMessage = $"Test method `{testMethod.TestClass.Class.Name}." +
					$"{method.Name}` has multiple [Label]-derived attributes";
			} else if (labelAttributes.Item1 is IReflectionAttributeInfo reflectLabel
					&& reflectLabel.Attribute is TLabelAttribute tlabelAttribute) {
				var map = _CurrentTestNumbers;
				if (map is null) {
					if (ThisAssembly.Debug) {
						DiagnosticMessageSink.OnMessage(new DiagnosticMessage(
							$"`{nameof(FindTestsForMethod)}()` called outside of `{nameof(FindTestsForType)}()`"));
					}
					goto Success;
				}
				int tnum = tlabelAttribute.TestNumber;
				if (tnum < 0 || map.TryAdd(tnum, method)) {
					goto Success;
				}
				errorMessage = $"Test class `{testMethod.TestClass.Class.Name}`" +
					$" has multiple test methods with the same test number: " +
					$"`{method.Name}()` conflicts with `{map[tnum].Name}()`";
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
