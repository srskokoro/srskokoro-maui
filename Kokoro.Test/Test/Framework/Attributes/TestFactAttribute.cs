namespace Kokoro.Test.Framework.Attributes;
using Kokoro.Test.Framework.Discovery;
using System;
using Xunit.Sdk;

[XunitTestCaseDiscoverer(TestFactDiscoverer.TypeName, ThisAssembly.Name)]
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class TestFactAttribute : SkippableFactAttribute, ITestFactAttribute {

	public TestFactAttribute(params Type[] skippingExceptions) : base(skippingExceptions) { }
}
