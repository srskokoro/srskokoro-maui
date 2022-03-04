namespace Kokoro.Test.Framework.TestCases;
using Xunit.Abstractions;
using Xunit.Sdk;

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
		return TestFactTestCase.GetDisplayName(TestMethod, TestMethodArguments, MethodGenericTypes, displayName, DefaultMethodDisplay);
	}
}
