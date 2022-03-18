namespace Kokoro.Test.Framework.TestCases;
using Kokoro.Test.Framework.Attributes;
using System.ComponentModel;
using Xunit.Abstractions;
using Xunit.Sdk;

public class TestFactTestCase : SkippableFactTestCase, ITestFactTestCase {

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

	public static string GetDisplayName(ITestMethod testMethod, object[] arguments, ITypeInfo[] genericTypes, string fallbackDisplayName, TestMethodDisplay methodDisplay) {
		var method = testMethod.Method;

		var attributeInfo = method.GetCustomAttributes(typeof(LabelAttribute)).FirstOrDefault();
		if (attributeInfo is IReflectionAttributeInfo reflectInfo) {
			var labelAttribute = (LabelAttribute)reflectInfo.Attribute;
			return labelAttribute.GetDisplayName(testMethod, arguments, genericTypes, methodDisplay);
		}

		return method.GetDisplayNameWithArguments(fallbackDisplayName, arguments, genericTypes);
	}

	protected override string GetDisplayName(IAttributeInfo factAttribute, string displayName) {
		return GetDisplayName(TestMethod, TestMethodArguments, MethodGenericTypes, displayName, DefaultMethodDisplay);
	}
}
