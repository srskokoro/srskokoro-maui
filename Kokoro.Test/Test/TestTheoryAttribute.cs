namespace Kokoro.Test;

using Kokoro.Test.Framework;
using System;
using Xunit.Sdk;

[XunitTestCaseDiscoverer(TestTheoryDiscoverer.TypeName, ThisAssembly.Name)]
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class TestTheoryAttribute : SkippableTheoryAttribute {

	public TestTheoryAttribute(params Type[] skippingExceptions) : base(skippingExceptions) { }
}
