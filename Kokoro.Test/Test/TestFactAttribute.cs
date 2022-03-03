namespace Kokoro.Test;

using Kokoro.Test.Framework;
using System;
using Xunit.Sdk;

[XunitTestCaseDiscoverer(TestFactDiscoverer.TypeName, ThisAssembly.Name)]
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class TestFactAttribute : SkippableFactAttribute {

	public TestFactAttribute(params Type[] skippingExceptions) : base(skippingExceptions) { }
}
