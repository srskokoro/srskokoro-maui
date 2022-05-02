namespace Kokoro.Common.Dispose;
using System.Runtime.Serialization;

internal static class DisposeUtils {

	public static ObjectDisposedException OdeFor<T>(in T obj) {
		return Ode(Var.TypeOf(in obj));
	}

	public static ObjectDisposedException Ode<T>() {
		return Ode(typeof(T));
	}

	public static ObjectDisposedException Ode(Type type) {
		// See also, https://stackoverflow.com/q/1964496
		return new(type.ToString());
	}

	public static ObjectDisposedException OdeNamed(string objectName) {
		return new(objectName);
	}


	public static void DisposeSafely<T>(this T disposable) where T : IDisposable {
		try {
			disposable.Dispose();
		} catch (Exception ex) {
			// Swallow
			Trace.WriteLine(ex);
		}
	}

	[Obsolete("Purpose and usage often misleading: it's not clear whether it " +
		"would throw or swallow the exception on disposal failure, as the " +
		"behavior depends on whether or not we're on a DEBUG build.")]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void DisposeSafely<T>(this T disposable, Exception priorException) where T : IDisposable {
#if !DEBUG
		disposable.DisposeSafely();
#else
		disposable.DisposePriorThrow(priorException);
#endif
	}

	public static void DisposePriorThrow<T>(this T disposable, Exception priorException) where T : IDisposable {
		try {
			disposable.Dispose();
		} catch (Exception ex) {
			throw new DisposeAggregateException(priorException, ex);
		}
	}

	// TODO Add an overload that uses a specialized collection instead that throws `DisposeAggregateException`
	public static void DisposeSafely<T>(this T disposable, ref ICollection<Exception>? priorExceptions) where T : IDisposable {
		try {
			disposable.Dispose();
		} catch (Exception ex) {
			// TODO Use an even more lighter alternative: a simpler linked-list node.
			ExceptionUtils.Collect(ref priorExceptions, ex);
		}
	}
}

public class DisposeAggregateException : AggregateException {

	public DisposeAggregateException() { }

	public DisposeAggregateException(IEnumerable<Exception> innerExceptions) : base(innerExceptions) { }

	public DisposeAggregateException(params Exception[] innerExceptions) : base(innerExceptions) { }

	public DisposeAggregateException(string? message) : base(message) { }

	public DisposeAggregateException(string? message, IEnumerable<Exception> innerExceptions) : base(message, innerExceptions) { }

	public DisposeAggregateException(string? message, Exception innerException) : base(message, innerException) { }

	public DisposeAggregateException(string? message, params Exception[] innerExceptions) : base(message, innerExceptions) { }

	protected DisposeAggregateException(SerializationInfo info, StreamingContext context) : base(info, context) { }
}
