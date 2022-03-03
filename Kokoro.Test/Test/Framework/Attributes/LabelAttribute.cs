namespace Kokoro.Test.Framework.Attributes;

using System;
using System.Text;
using Xunit.Sdk;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
internal class LabelAttribute : Attribute {
	public const string DefaultLabelSeparator = " :: ";

	public virtual string? Text { get; set; }

	public virtual string? TestMethodNameOverride { get; set; }

	public virtual string? LabelSeparator { get; set; }

	protected LabelAttribute() { }

	public LabelAttribute(string? text, string? testMethodNameOverride = null, string? labelSeparator = null) {
		Text = text;
		TestMethodNameOverride = testMethodNameOverride;
		LabelSeparator = labelSeparator;
	}

	public virtual string GetDisplayName(ITestMethod testMethod, object[]? testMethodArguments, ITypeInfo[]? methodGenericTypes, TestMethodDisplay testMethodDisplay) {
		var baseDisplayNameBuilder = new StringBuilder();

		var method = testMethod.Method;
		string? methodName;

		if (testMethodDisplay != TestMethodDisplay.ClassAndMethod) {
			methodName = TestMethodNameOverride ?? method.Name;
		} else {
			baseDisplayNameBuilder.Append(testMethod.TestClass.Class.Name);

			methodName = TestMethodNameOverride;
			if (methodName is null) {
				methodName = method.Name;
			} else if (methodName.Length == 0) {
				// Empty method name; Let it be just the class name then
				goto AppendMethodName; // Omit the dot (below)
			}

			baseDisplayNameBuilder.Append('.');
		}

	AppendMethodName:
		baseDisplayNameBuilder.Append(methodName);

		string labelSeparator;
		if (Text is string text) {
			labelSeparator = LabelSeparator ?? DefaultLabelSeparator;
			if (baseDisplayNameBuilder.Length != 0) {
				baseDisplayNameBuilder.Append(labelSeparator);
			}
			baseDisplayNameBuilder.Append(text);
		} else {
			labelSeparator = "";
		}

		if (testMethodArguments?.Length > 0 || methodGenericTypes?.Length > 0) {
			// `baseDisplayNameBuilder` may currently be empty: let it be then
			baseDisplayNameBuilder.Append(labelSeparator);

			string baseDisplayName = baseDisplayNameBuilder.ToString();
			return method.GetDisplayNameWithArguments(baseDisplayName, testMethodArguments, methodGenericTypes);
		}

		return baseDisplayNameBuilder.ToString();
	}
}
