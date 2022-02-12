using System.Runtime.CompilerServices;

namespace Kokoro.Util;

internal static class DisposeUtil {

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


	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void DisposeSafely<T>(this T disposable) where T : IDisposable {
		try {
			disposable.Dispose();
		} catch (Exception ex) {
			// Swallow
			Debug.WriteLine(ex);
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void DisposePriorThrow<T>(this T disposable, Exception priorException) where T : IDisposable {
		try {
			disposable.Dispose();
		} catch (Exception ex) {
			throw new AggregateException(priorException, ex);
		}
	}
}
