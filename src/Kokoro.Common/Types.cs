namespace Kokoro.Common;
using System;

internal static class Types {

	/// <summary>
	/// Gets the type of a variable or value without needing to box value types.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Type TypeOf<T>(in T var) {
		return typeof(T).IsValueType || var == null ? typeof(T) : var.GetType();
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsReferenceOrContainsReferences<T>(
		[SuppressMessage("Style", "IDE0060:Remove unused parameter")] in T var
	) => RuntimeHelpers.IsReferenceOrContainsReferences<T>();

	[Conditional("DEBUG")]
	public static void DAssert_IsReferenceOrContainsReferences<T>(in T var)
		=> Debug.Assert(IsReferenceOrContainsReferences(var));
}
