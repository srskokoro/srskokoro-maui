namespace Kokoro.Test.Framework;
using System.Reflection;
using Xunit.Sdk;

internal partial class TestFramework : XunitTestFramework {

	public TestFramework(IMessageSink messageSink) : base(messageSink) { }

	protected override ITestFrameworkExecutor CreateExecutor(AssemblyName assemblyName)
		=> new TestFrameworkExecutor(assemblyName, SourceInformationProvider, DiagnosticMessageSink);
}
