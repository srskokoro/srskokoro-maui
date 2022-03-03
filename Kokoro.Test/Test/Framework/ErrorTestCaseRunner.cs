namespace Kokoro.Test.Framework;

using System.Threading;
using System.Threading.Tasks;
using Xunit.Sdk;

/// <summary>
/// An implementation of <see cref="TestCaseRunner{TTestCase}"/> that can be
/// used to report an error rather than running a test. This is a generalized
/// alternative to <see cref="ExecutionErrorTestCaseRunner"/>, to support any
/// <see cref="IXunitTestCase"/> implementation.
/// </summary>
public class ErrorTestCaseRunner<TXunitTestCase>
	: TestCaseRunner<TXunitTestCase> where TXunitTestCase : IXunitTestCase {

	/// <param name="testCase">The test case that the lambda represents.</param>
	/// <param name="messageBus">The message bus to report run status to.</param>
	/// <param name="aggregator">The exception aggregator used to run code and collect exceptions.</param>
	/// <param name="cancellationTokenSource">The task cancellation token source, used to cancel the test run.</param>
	public ErrorTestCaseRunner(
		TXunitTestCase testCase,
		string errorMessage,
		IMessageBus messageBus,
		ExceptionAggregator aggregator,
		CancellationTokenSource cancellationTokenSource
	) : base(testCase, messageBus, aggregator, cancellationTokenSource) {
		ErrorMessage = errorMessage;
	}

	/// <summary>
	/// Gets the error message that will be display when the test is run.
	/// </summary>
	public string ErrorMessage { get; private set; }

	/// <inheritdoc/>
	protected override Task<RunSummary> RunTestAsync() {
		var test = new XunitTest(TestCase, TestCase.DisplayName);
		var summary = new RunSummary { Total = 1 };

		if (!MessageBus.QueueMessage(new TestStarting(test)))
			CancellationTokenSource.Cancel();
		else {
			summary.Failed = 1;

			var testFailed = new TestFailed(test, 0, null,
				new[] { typeof(InvalidOperationException).FullName },
				new[] { ErrorMessage },
				new[] { "" },
				new[] { -1 }
			);

			if (!MessageBus.QueueMessage(testFailed))
				CancellationTokenSource.Cancel();

			if (!MessageBus.QueueMessage(new TestFinished(test, 0, null)))
				CancellationTokenSource.Cancel();
		}

		return Task.FromResult(summary);
	}
}
