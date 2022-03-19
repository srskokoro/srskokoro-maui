namespace Kokoro.Common.Util;

internal static class Var {

	/// <summary>
	/// Gets the type of a variable or value without needing to box value types.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static Type TypeOf<T>(in T var) {
		return typeof(T).IsValueType || var == null ? typeof(T) : var.GetType();
	}
}
