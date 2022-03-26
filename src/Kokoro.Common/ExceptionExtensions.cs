namespace Kokoro.Common;

internal static class ExceptionExtensions {

	/// <summary>
	/// Throws the exception with the original stack trace preserved.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[DoesNotReturn]
	public static void ReThrow(this Exception exception)
		=> ExceptionDispatchInfo.Throw(exception);

	/// <summary>
	/// Throws the <see cref="Exception.InnerException">inner exception</see>
	/// if any. If there's no inner exception, throws the exception itself. Any
	/// existing stack trace information is preserved.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[DoesNotReturn]
	public static void ReThrowInner(this Exception exception)
		=> ExceptionDispatchInfo.Throw(exception.InnerException ?? exception);

	/// <summary>
	/// Throws the exception with the original stack trace preserved. If the
	/// exception is an <see cref="AggregateException"/>, the <see cref="AggregateException.Flatten">flattened</see>
	/// exception is thrown instead.
	/// </summary>
	[DoesNotReturn]
	public static void ReThrowFlatten(this Exception exception) {
		if (exception is AggregateException aggrEx && aggrEx.ShouldFlatten()) {
			exception = aggrEx.Flatten();
		}
		ExceptionDispatchInfo.Throw(exception);
	}

	/// <summary>
	/// Combines the exceptions into a single <see cref="AggregateException"/>,
	/// then throws. If there's only one exception, that exception is throw
	/// instead, preserving the original stack trace. If there's no exception,
	/// nothing is thrown.
	/// </summary>
	public static void ReThrow(this IEnumerable<Exception> exceptions) {
		if (!exceptions.TryGetNonEnumeratedCount(out int count)) {
			count = exceptions.Count();
		}
		if (count == 0) return;
		ExceptionDispatchInfo.Throw(count == 1 ?
			exceptions.Single() : new AggregateException(exceptions));
	}

	/// <summary>
	/// Similar to <see cref="ReThrow(IEnumerable{Exception})"/> but flattens
	/// the resulting <see cref="AggregateException"/> before throwing.
	/// </summary>
	public static void ReThrowFlatten(this IEnumerable<Exception> exceptions) {
		if (!exceptions.TryGetNonEnumeratedCount(out int count)) {
			count = exceptions.Count();
		}
		if (count == 0) return;

		Exception ex;
		if (count == 1) {
			ex = exceptions.Single();
		} else {
			var aggrEx = new AggregateException(exceptions);
			ex = exceptions.ShouldFlatten() ? aggrEx.Flatten() : aggrEx;
		}

		ExceptionDispatchInfo.Throw(ex);
	}

	// --

	public static bool ShouldFlatten(this IEnumerable<Exception> exceptions) {
		foreach (var ex in exceptions) {
			if (ex is AggregateException) {
				return true;
			}
		}
		return false;
	}

	public static bool ShouldFlatten(this AggregateException aggregateException)
		=> aggregateException.InnerExceptions.ShouldFlatten();

	public static AggregateException FlattenIfNeeded(this AggregateException aggregateException)
		=> aggregateException.ShouldFlatten() ? aggregateException.Flatten() : aggregateException;
}
