namespace Kokoro.Test.Framework;
using System.Reflection;
using Xunit.Sdk;
using TestFrameworkDiscoverer = Discovery.TestFrameworkDiscoverer;

public partial class TestFramework : XunitTestFramework {

	public TestFramework(IMessageSink messageSink) : base(messageSink) { }

	protected override ITestFrameworkDiscoverer CreateDiscoverer(IAssemblyInfo assemblyInfo)
		=> new TestFrameworkDiscoverer(assemblyInfo, SourceInformationProvider, DiagnosticMessageSink);

	protected override ITestFrameworkExecutor CreateExecutor(AssemblyName assemblyName)
		=> new TestFrameworkExecutor(assemblyName, SourceInformationProvider, DiagnosticMessageSink);
}
