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
}
