namespace Kokoro.Test.Framework.Discovery;
using Kokoro.Test.Framework.TestCases;
using Xunit.Abstractions;
using Xunit.Sdk;

public class TestFactDiscoverer : FactDiscoverer {

	public const string TypeNamespace = $"Kokoro.Test.Framework.Discovery";

	public const string TypeName = $"{TypeNamespace}.{nameof(TestFactDiscoverer)}";

	/// <param name="diagnosticMessageSink">The message sink used to send diagnostic messages.</param>
	public TestFactDiscoverer(IMessageSink diagnosticMessageSink) : base(diagnosticMessageSink) { }

	/// <inheritdoc />
	protected override IXunitTestCase CreateTestCase(ITestFrameworkDiscoveryOptions discoveryOptions, ITestMethod testMethod, IAttributeInfo factAttribute) {
		string[] skippingExceptionNames = SkippableFactDiscoverer.GetSkippableExceptionNames(factAttribute);
		return new TestFactTestCase(
			skippingExceptionNames,
			DiagnosticMessageSink,
			discoveryOptions.MethodDisplayOrDefault(),
			discoveryOptions.MethodDisplayOptionsOrDefault(),
			testMethod
		);
	}
}
