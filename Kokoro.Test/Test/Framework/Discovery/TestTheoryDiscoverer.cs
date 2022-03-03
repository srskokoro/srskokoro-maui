namespace Kokoro.Test.Framework.Discovery;
using Kokoro.Test.Framework.TestCases;
using System.Collections.Generic;
using Xunit.Abstractions;
using Xunit.Sdk;

public class TestTheoryDiscoverer : TheoryDiscoverer {

	public const string TypeNamespace = $"Kokoro.Test.Framework.Discovery";

	public const string TypeName = $"{TypeNamespace}.{nameof(TestTheoryDiscoverer)}";

	public TestTheoryDiscoverer(IMessageSink diagnosticMessageSink) : base(diagnosticMessageSink) { }

	protected override IEnumerable<IXunitTestCase> CreateTestCasesForDataRow(ITestFrameworkDiscoveryOptions discoveryOptions, ITestMethod testMethod, IAttributeInfo theoryAttribute, object[] dataRow) {
		return new[] {
			new TestFactTestCase(
				GetSkippableExceptionNames(theoryAttribute),
				DiagnosticMessageSink,
				discoveryOptions.MethodDisplayOrDefault(),
				discoveryOptions.MethodDisplayOptionsOrDefault(),
				testMethod,
				dataRow
			),
		};
	}

	protected override IEnumerable<IXunitTestCase> CreateTestCasesForTheory(ITestFrameworkDiscoveryOptions discoveryOptions, ITestMethod testMethod, IAttributeInfo theoryAttribute) {
		return new[] {
			new TestTheoryTestCase(
				GetSkippableExceptionNames(theoryAttribute),
				DiagnosticMessageSink,
				discoveryOptions.MethodDisplayOrDefault(),
				discoveryOptions.MethodDisplayOptionsOrDefault(),
				testMethod
			),
		};
	}

	[ThreadStatic]
	private static (IAttributeInfo TheoryAttribute, string[] SkippingExceptionNames) _CachedPair_AttributeToSkippingExceptionNames;

	private static string[] GetSkippableExceptionNames(IAttributeInfo theoryAttribute) {
		ref var cached = ref _CachedPair_AttributeToSkippingExceptionNames;
		if (cached.TheoryAttribute != theoryAttribute || cached.SkippingExceptionNames is null) {
			cached = (theoryAttribute, SkippableFactDiscoverer.GetSkippableExceptionNames(theoryAttribute));
			_CachedPair_AttributeToSkippingExceptionNames = cached;
		}
		return cached.SkippingExceptionNames;
	}

	public override IEnumerable<IXunitTestCase> Discover(ITestFrameworkDiscoveryOptions discoveryOptions, ITestMethod testMethod, IAttributeInfo theoryAttribute) {
		try {
			return base.Discover(discoveryOptions, testMethod, theoryAttribute);
		} finally {
			_CachedPair_AttributeToSkippingExceptionNames = default;
		}
	}
}
