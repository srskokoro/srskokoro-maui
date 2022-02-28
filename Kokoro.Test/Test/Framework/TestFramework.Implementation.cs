namespace Kokoro.Test.Framework;

using System.Reflection;
using Xunit.Sdk;

internal partial class TestFramework : XunitTestFramework {

	public TestFramework(IMessageSink messageSink) : base(messageSink) { }

	protected override ITestFrameworkExecutor CreateExecutor(AssemblyName assemblyName)
		=> new TestFrameworkExecutor(assemblyName, SourceInformationProvider, DiagnosticMessageSink);
}

internal class TestFrameworkExecutor : XunitTestFrameworkExecutor, IRandomizedTestFramework {

	public TestFrameworkExecutor(
		AssemblyName assemblyName,
		ISourceInformationProvider sourceInformationProvider,
		IMessageSink diagnosticMessageSink
	) : base(assemblyName, sourceInformationProvider, diagnosticMessageSink) { }

	protected override async void RunTestCases(
		IEnumerable<IXunitTestCase> testCases,
		IMessageSink executionMessageSink,
		ITestFrameworkExecutionOptions executionOptions
	) {
		LoadLocalRandomState();
		using var assemblyRunner = new XunitTestAssemblyRunner(TestAssembly, testCases, DiagnosticMessageSink, executionMessageSink, executionOptions);
		RunSummary testSummary = await assemblyRunner.RunAsync();
		SaveLocalRandomState(testSummary);
	}

	// --

	private protected const string DateTimeSeedFile = @"test_start_dt_preserved_on_fail.dat";

	private protected static void LoadLocalRandomState() {
		IRandomizedTestFramework.LoadLocalRandomState(TestFrameworkConfig.DateTimeSeed, DateTimeSeedFile);
	}

	private protected static void SaveLocalRandomState(RunSummary testSummary) {
		IRandomizedTestFramework.SaveLocalRandomState(testSummary, DateTimeSeedFile);
	}
}
