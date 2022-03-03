namespace Kokoro.Test.Framework.TestCases;
using Kokoro.Test.Framework.Attributes;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Xunit.Abstractions;
using Xunit.Sdk;

public class TestFactTestCase : SkippableFactTestCase {

	[EditorBrowsable(EditorBrowsableState.Never)]
	[Obsolete("Called by the de-serializer", true)]
	public TestFactTestCase() { }

	[Obsolete("Please call the constructor which takes `TestMethodDisplayOptions`")]
	public TestFactTestCase(
		string[] skippingExceptionNames,
		IMessageSink diagnosticMessageSink,
		TestMethodDisplay defaultMethodDisplay,
		ITestMethod testMethod,
		object[]? testMethodArguments = null
	) : base(skippingExceptionNames, diagnosticMessageSink, defaultMethodDisplay, testMethod, testMethodArguments) { }

	public TestFactTestCase(
		string[] skippingExceptionNames,
		IMessageSink diagnosticMessageSink,
		TestMethodDisplay defaultMethodDisplay,
		TestMethodDisplayOptions defaultMethodDisplayOptions,
		ITestMethod testMethod,
		object[]? testMethodArguments = null
	) : base(skippingExceptionNames, diagnosticMessageSink, defaultMethodDisplay, defaultMethodDisplayOptions, testMethod, testMethodArguments) { }

	internal static bool ResolveDisplayName(out string resolvedDisplayName, ITestMethod testMethod, object[] arguments, ITypeInfo[] genericTypes, string fallbackDisplayName, TestMethodDisplay methodDisplay) {
		var method = testMethod.Method;

		var labelAttributes = method.GetCustomAttributes(typeof(LabelAttribute)).CastOrToList();
		int labelAttributesCount = labelAttributes.Count;

		if (labelAttributesCount == 1) {
			if (GetLabelAttribute(labelAttributes[0]) is LabelAttribute labelAttribute) {
				resolvedDisplayName = labelAttribute.GetDisplayName(testMethod, arguments, genericTypes, methodDisplay);
				return true;
			}
		}
		resolvedDisplayName = method.GetDisplayNameWithArguments(fallbackDisplayName, arguments, genericTypes);
		return labelAttributesCount <= 1; // `false` means error: too many label attributes
	}

	internal static LabelAttribute? GetLabelAttribute(IAttributeInfo attributeInfo) {
		return (attributeInfo as IReflectionAttributeInfo)?.Attribute as LabelAttribute;
	}

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

	internal static string GetErrorMessageForTooManyLabelAttributes(ITestMethod testMethod)
		=> $"Test method '{testMethod.TestClass.Class.Name}.{testMethod.Method.Name}' has multiple [Label]-derived attributes";

	public override Task<RunSummary> RunAsync(IMessageSink diagnosticMessageSink, IMessageBus messageBus, object[] constructorArguments, ExceptionAggregator aggregator, CancellationTokenSource cancellationTokenSource) {
		if (_ErrorTooManyLabelAttributes) {
			var message = GetErrorMessageForTooManyLabelAttributes(TestMethod);
			var runner = new ErrorTestCaseRunner<TestFactTestCase>(this, message, messageBus, aggregator, cancellationTokenSource);
			return runner.RunAsync();
		}
		return base.RunAsync(diagnosticMessageSink, messageBus, constructorArguments, aggregator, cancellationTokenSource);
	}
}
