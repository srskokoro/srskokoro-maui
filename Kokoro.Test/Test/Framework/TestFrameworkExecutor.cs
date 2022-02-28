namespace Kokoro.Test.Framework;

using System.Reflection;
using Xunit.Sdk;

internal class TestFrameworkExecutor : XunitTestFrameworkExecutor, IRandomizedTestEstablisher {

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
		IRandomizedTestEstablisher.LoadLocalRandomState(TestFrameworkConfig.DateTimeSeed, DateTimeSeedFile);
	}

	private protected static void SaveLocalRandomState(RunSummary testSummary) {
		IRandomizedTestEstablisher.SaveLocalRandomState(testSummary, DateTimeSeedFile);
	}
}
