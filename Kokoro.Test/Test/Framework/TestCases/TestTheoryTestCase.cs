namespace Kokoro.Test.Framework.TestCases;
using System.Threading;
using System.Threading.Tasks;
using Xunit.Abstractions;
using Xunit.Sdk;

using static TestFactTestCase;

public class TestTheoryTestCase : SkippableTheoryTestCase, ITestFactTestCase {

	[Obsolete("Called by the de-serializer", true)]
	public TestTheoryTestCase() { }

	[Obsolete("Please call the constructor which takes `TestMethodDisplayOptions`")]
	public TestTheoryTestCase(
		string[] skippingExceptionNames,
		IMessageSink diagnosticMessageSink,
		TestMethodDisplay defaultMethodDisplay,
		ITestMethod testMethod
	) : base(skippingExceptionNames, diagnosticMessageSink, defaultMethodDisplay, testMethod) { }

	public TestTheoryTestCase(
		string[] skippingExceptionNames,
		IMessageSink diagnosticMessageSink,
		TestMethodDisplay defaultMethodDisplay,
		TestMethodDisplayOptions defaultMethodDisplayOptions,
		ITestMethod testMethod
	) : base(skippingExceptionNames, diagnosticMessageSink, defaultMethodDisplay, defaultMethodDisplayOptions, testMethod) { }

	protected override string GetDisplayName(IAttributeInfo factAttribute, string displayName) {
		_ErrorTooManyLabelAttributes = !ResolveDisplayName(
			out string resolvedDisplayName,
			TestMethod,
			TestMethodArguments,
			MethodGenericTypes,
			displayName,
			DefaultMethodDisplay
		);
		return resolvedDisplayName;
	}

	private bool _ErrorTooManyLabelAttributes;

	public override Task<RunSummary> RunAsync(IMessageSink diagnosticMessageSink, IMessageBus messageBus, object[] constructorArguments, ExceptionAggregator aggregator, CancellationTokenSource cancellationTokenSource) {
		if (_ErrorTooManyLabelAttributes) {
			var message = GetErrorMessageForTooManyLabelAttributes(TestMethod);
			var runner = new ErrorTestCaseRunner<TestTheoryTestCase>(this, message, messageBus, aggregator, cancellationTokenSource);
			return runner.RunAsync();
		}
		return base.RunAsync(diagnosticMessageSink, messageBus, constructorArguments, aggregator, cancellationTokenSource);
	}
}
