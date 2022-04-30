namespace Kokoro.Test.Util;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using Xunit.Sdk;

/// <summary>
/// An <see cref="IAssertionStrategy"/> that captures stack trace information
/// with each call to <see cref="HandleFailure(string)">HandleFailure()</see>
/// and preserves this information within <see cref="Exception"/> objects,
/// thrown collectively as a single <see cref="AggregateException"/> when
/// <see cref="ThrowIfAny(IDictionary{string, object})">ThrowIfAny()</see> is
/// called.
/// </summary>
public class AssertionCaptureStrategy : IAssertionStrategy {
	private readonly List<Exception> _Exceptions;
	private readonly IEnumerable<string> _Messages;

	[MethodImpl(MethodImplOptions.NoInlining)]
	// ^ Prevent inlining from affecting where the stack trace starts;
	// necessary since we expect to skip this constructor from the stack trace.
	public AssertionCaptureStrategy() {
		_Exceptions = new();
		_Messages = _Exceptions.Select(ex => ex.Message);

		// Skip, not only the current stack frame, but also the caller's…
		const int SkipFrames = 2;
		// ^ The above will ensure that the caller's stack frame is always
		// included in the resulting stack trace captured by `HandleFailure()`.
		//
		// Otherwise, the caller's stack trace could end up being a suffix
		// string of the stack trace captured by `HandleFailure()`, and if so,
		// upon chopping off the common suffix, the caller's stack frame will
		// be removed as well. See also `HandleFailure()`
		// --

		ReferenceStackTrace = new StackTrace(SkipFrames, true).ToString().TrimEnd();
		// ^ Omits trailing newlines -- (a bit) similar to `Environment.StackTrace`
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	// ^ Prevent inlining from affecting where the stack trace starts;
	// necessary since we expect to skip this method from the stack trace.
	public void UpdateReferenceStackTrace(int extraSkipFrames = 0) {
		// Skip, not only the current stack frame, but also the caller's…
		int skipFrames = 2 + extraSkipFrames;
		// ^ As for why, see the same comment in the constructor code.

		ReferenceStackTrace = new StackTrace(skipFrames, true).ToString().TrimEnd();
		// ^ Omits trailing newlines -- (a bit) similar to `Environment.StackTrace`
	}

	// --

	public string ReferenceStackTrace;

	public IEnumerable<Exception> Exceptions => _Exceptions;

	public IEnumerable<string> FailureMessages => _Messages;

	[MethodImpl(MethodImplOptions.NoInlining)]
	// ^ Prevent inlining from affecting where the stack trace starts;
	// necessary since we expect to skip this method from the stack trace.
	public void HandleFailure(string message) {
		var stRef = ReferenceStackTrace.AsSpan();

		ref char x = ref MemoryMarshal.GetReference(stRef);
		int i = stRef.Length;

		// Omit trailing newlines -- (a bit) similar to `Environment.StackTrace`
		var stCur = new StackTrace(skipFrames: 1, true).ToString().AsSpan().TrimEnd();

		ref char y = ref MemoryMarshal.GetReference(stCur);
		int j = stCur.Length;

		bool swap;
		if (swap = i > j) {
			(i, j) = (j, i);
			// Swap refs
			{
				ref char//
				_ = ref x;
				x = ref y;
				y = ref _;
			}
		}

		// Chop off stack frames at the end of the current stack trace that are
		// also present at the end of the reference stack trace.
		//
		// Also, the final newline will be omitted from the resulting stack
		// trace string -- this is similar to `Environment.StackTrace`, which
		// omits the trailing newline.
		for (; ; ) {
			if (i <= 0) {
				var nl = Environment.NewLine;
				int nl_len = nl.Length;
				int d = swap ? i : j;
				if (d >= nl_len && stCur.Slice(d-nl_len, nl_len).SequenceEqual(nl)) {
					d -= nl_len; // Omit the trailing newline
				}
				stCur = stCur[..d];
				break; // No more characters to compare
			}
			if (U.Add(ref x, --i) != U.Add(ref y, --j)) {
				int d = swap ? i : j;
				int p = stCur[d..].IndexOf(Environment.NewLine);
				if (p >= 0) {
					stCur = stCur[..(d+p)]; // Omit the trailing newline
				}
				break; // Split already found
			}
		}

		var ex = new XunitException(message);
		ExceptionDispatchInfo.SetRemoteStackTrace(ex, stCur.ToString());
		HandleFailure(ex);
	}

	public void HandleFailure(Exception ex) {
		_Exceptions.Add(ex);
	}

	public IEnumerable<string> DiscardFailures() {
		var discards = _Messages.ToArray();
		ClearFailures();
		return discards;
	}

	public void ClearFailures() {
		_Exceptions.Clear();
	}

	public void ThrowIfAny(IDictionary<string, object>? context = null) {
		if (AggregateIfAny(context) is Exception ex) {
			ExceptionDispatchInfo.Throw(ex);
		}
	}

	public Exception? AggregateIfAny(IDictionary<string, object>? context = null) {
		var exceptions = _Exceptions;
		if (exceptions.Count > 0) {

			string mainMessage;
			if (context == null || context.Count <= 0) {
				mainMessage = "";
			} else {
				const string Indent = "    ";
				var fp = CultureInfo.InvariantCulture;
				var sb = new StringBuilder();
				sb.AppendLine();
				foreach (var pair in context) {
					sb.AppendLine(fp, $"{Indent}--");
					sb.AppendLine(fp, $"{Indent}With key: {pair.Key}");
					sb.AppendLine(fp, $"{Indent}   value: {pair.Value}");
				}
				sb.AppendLine(fp, $"{Indent}--");
				mainMessage = sb.ToString();
			}

			return exceptions.Count == 1 ? exceptions[0]
				: new MinimalAggregateException(mainMessage, exceptions);
		}
		return null;
	}

	public void ClearAndThrowIfAny(IDictionary<string, object>? context = null) {
		try {
			ThrowIfAny(context);
		} finally {
			ClearFailures();
		}
	}

	public Exception? ClearAndAggregateIfAny(IDictionary<string, object>? context = null) {
		try {
			return AggregateIfAny(context);
		} finally {
			ClearFailures();
		}
	}
}
