namespace Kokoro.Test.Util;
using FluentAssertions.Formatting;

/// <summary>
/// An <see cref="IAssertionScope">AssertionScope</see> that captures stack
/// trace information via <see cref="AssertionCaptureStrategy"/>.
/// </summary>
public sealed class AssertionCapture : IAssertionScope {

	public readonly AssertionScope Scope;
	public readonly AssertionCaptureStrategy Strategy;

	public AssertionCapture() {
		Scope = new AssertionScope(Strategy = new AssertionCaptureStrategy());
	}

	public AssertionCapture(string? context) : this() {
		if (!string.IsNullOrEmpty(context)) {
			Scope.Context = new Lazy<string>(() => context);
		}
	}

	public AssertionCapture(Lazy<string>? context)
		: this() => Scope.Context = context;

	// --

	#region `AssertionScope` delegations

	/// <summary>
	/// Gets or sets the context of the current assertion scope, e.g. the path of the object graph
	/// that is being asserted on. The context is provided by a <see cref="Lazy{String}"/> which
	/// only gets evaluated when its value is actually needed (in most cases during a failure).
	/// </summary>
	public Lazy<string> Context { get => Scope.Context; set => Scope.Context = value; }

	/// <summary>
	/// Exposes the options the scope will use for formatting objects in case an assertion fails.
	/// </summary>
	public FormattingOptions FormattingOptions => Scope.FormattingOptions;

	/// <summary>
	/// Adds an explanation of why the assertion is supposed to succeed to the scope.
	/// </summary>
	public AssertionScope BecauseOf(Reason reason) => Scope.BecauseOf(reason);

	/// <summary>
	/// Makes assertion fail when <paramref name="actualOccurrences"/> does not match <paramref name="constraint"/>.
	/// <para>
	/// The occurrence description in natural language could then be inserted in failure message by using
	/// <em>{expectedOccurrence}</em> placeholder in message parameters of <see cref="FailWith(string, object[])"/> and its
	/// overloaded versions.
	/// </para>
	/// </summary>
	/// <param name="constraint"><see cref="OccurrenceConstraint"/> defining the number of expected occurrences.</param>
	/// <param name="actualOccurrences">The number of actual occurrences.</param>
	public AssertionScope ForConstraint(OccurrenceConstraint constraint, int actualOccurrences) => Scope.ForConstraint(constraint, actualOccurrences);

	/// <summary>
	/// Gets the identity of the caller associated with the current scope.
	/// </summary>
	public string CallerIdentity => Scope.CallerIdentity;

	/// <summary>
	/// Adds a pre-formatted failure message to the current scope.
	/// </summary>
	public void AddPreFormattedFailure(string formattedFailureMessage) => Scope.AddPreFormattedFailure(formattedFailureMessage);

	/// <summary>
	/// Adds a block of tracing to the scope for reporting when an assertion fails.
	/// </summary>
	public void AppendTracing(string tracingBlock) => Scope.AppendTracing(tracingBlock);

	/// <summary>
	/// Tracks a keyed object in the current scope that is excluded from the failure message in case an assertion fails.
	/// </summary>
	public void AddNonReportable(string key, object value) => Scope.AddNonReportable(key, value);

	/// <summary>
	/// Adds some information to the assertion scope that will be included in the message
	/// that is emitted if an assertion fails.
	/// </summary>
	public void AddReportable(string key, string value) => Scope.AddReportable(key, value);

	/// <summary>
	/// Adds some information to the assertion scope that will be included in the message
	/// that is emitted if an assertion fails. The value is only calculated on failure.
	/// </summary>
	public void AddReportable(string key, Func<string> valueFunc) => Scope.AddReportable(key, valueFunc);

	public bool HasFailures() => Scope.HasFailures();

	/// <summary>
	/// Gets data associated with the current scope and identified by <paramref name="key"/>.
	/// </summary>
	public T Get<T>(string key) => Scope.Get<T>(key);

	/// <summary>
	/// Allows the scope to assume that all assertions that happen within this scope are going to
	/// be initiated by the same caller.
	/// </summary>
	public void AssumeSingleCaller() => Scope.AssumeSingleCaller();

	#endregion

	#region `IAssertionScope` implementation

	public Continuation ClearExpectation() => Scope.ClearExpectation();

	public GivenSelector<T> Given<T>(Func<T> selector) => Scope.Given(selector);

	public AssertionCapture ForCondition(bool condition) {
		Scope.ForCondition(condition);
		return this;
	}

	public Continuation FailWith(string message) => Scope.FailWith(message);

	public Continuation FailWith(Func<FailReason> failReasonFunc) => Scope.FailWith(failReasonFunc);

	public Continuation FailWith(string message, params object[] args) => Scope.FailWith(message, args);

	public Continuation FailWith(string message, params Func<object>[] argProviders) => Scope.FailWith(message, argProviders);

	public AssertionCapture BecauseOf(string because, params object[] becauseArgs) {
		Scope.BecauseOf(because, becauseArgs);
		return this;
	}

	public AssertionCapture WithExpectation(string message, params object[] args) {
		Scope.WithExpectation(message, args);
		return this;
	}

	public AssertionCapture WithDefaultIdentifier(string identifier) {
		Scope.WithDefaultIdentifier(identifier);
		return this;
	}

	/// <summary>
	/// Returns all failures that happened up to this point and ensures they will not cause
	/// <see cref="Dispose"/> to fail the assertion.
	/// </summary>
	public string[] Discard() => Scope.Discard();

	public AssertionCapture UsingLineBreaks {
		get {
			_ = Scope.UsingLineBreaks;
			return this;
		}
	}

	IAssertionScope IAssertionScope.ForCondition(bool condition) => ForCondition(condition);
	IAssertionScope IAssertionScope.BecauseOf(string because, params object[] becauseArgs) => BecauseOf(because, becauseArgs);
	IAssertionScope IAssertionScope.WithExpectation(string message, params object[] args) => WithExpectation(message, args);
	IAssertionScope IAssertionScope.WithDefaultIdentifier(string identifier) => WithDefaultIdentifier(identifier);
	IAssertionScope IAssertionScope.UsingLineBreaks => UsingLineBreaks;

	#endregion

	[StackTraceHidden]
	public void Dispose() => Scope.Dispose();
}
