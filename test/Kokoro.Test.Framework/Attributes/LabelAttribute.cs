namespace Kokoro.Test.Framework.Attributes;
using System;
using System.Text;
using Xunit.Sdk;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class LabelAttribute : Attribute {
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
		var sb = new StringBuilder();

		var method = testMethod.Method;
		string? methodName;

		if (testMethodDisplay != TestMethodDisplay.ClassAndMethod) {
			methodName = TestMethodNameOverride ?? method.Name;
		} else {
			sb.Append(testMethod.TestClass.Class.Name);

			methodName = TestMethodNameOverride;
			if (methodName == null) {
				methodName = method.Name;
			} else if (methodName.Length == 0) {
				// Empty method name; Let it be just the class name then
				goto AppendMethodName; // Omit the dot (below)
			}

			sb.Append('.');
		}

	AppendMethodName:
		sb.Append(methodName);

		string labelSeparator;
		if (Text is string text) {
			labelSeparator = LabelSeparator ?? DefaultLabelSeparator;
			if (sb.Length != 0) {
				sb.Append(labelSeparator);
			}
			sb.Append(text);
		} else {
			labelSeparator = "";
		}

		if (testMethodArguments?.Length > 0 || methodGenericTypes?.Length > 0) {
			// `baseDisplayNameBuilder` may currently be empty: let it be then
			sb.Append(labelSeparator);

			string baseDisplayName = sb.ToString();
			return method.GetDisplayNameWithArguments(baseDisplayName, testMethodArguments, methodGenericTypes);
		}

		return sb.ToString();
	}
}
