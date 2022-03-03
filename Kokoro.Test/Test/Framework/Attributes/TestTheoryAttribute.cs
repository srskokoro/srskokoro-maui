namespace Kokoro.Test.Framework.Attributes;
using Kokoro.Test.Framework.Discovery;
using System;
using Xunit.Sdk;

[XunitTestCaseDiscoverer(TestTheoryDiscoverer.TypeName, ThisAssembly.Name)]
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class TestTheoryAttribute : SkippableTheoryAttribute {

	public TestTheoryAttribute(params Type[] skippingExceptions) : base(skippingExceptions) { }
}
