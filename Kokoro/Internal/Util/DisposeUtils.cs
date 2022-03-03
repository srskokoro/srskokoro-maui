namespace Kokoro.Internal.Util;

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
			Debug.WriteLine(ex);
		}
	}

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
