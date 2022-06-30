namespace Kokoro.Common;

internal static class EnumUtils {

	internal static class EnumVals<TEnum> where TEnum : struct, Enum {
		internal static readonly TEnum[] Values = Enum.GetValues<TEnum>();
	}

	private static class EnumSet<TEnum> where TEnum : struct, Enum {
		internal static readonly HashSet<TEnum> Set = new(EnumVals<TEnum>.Values);
	}

	public static bool IsDefined<TEnum>(this TEnum enumValue) where TEnum : struct, Enum {
		// See also,
		// - https://stackoverflow.com/a/55028274
		// - https://stackoverflow.com/a/13635
		// - https://docs.microsoft.com/en-us/archive/blogs/brada/the-danger-of-over-simplification-enum-isdefined

		return EnumSet<TEnum>.Set.Contains(enumValue);
	}

	[Conditional("DEBUG")]
	public static void DAssert_Defined<TEnum>(this TEnum enumValue) where TEnum : struct, Enum {
		Debug.Assert(IsDefined(enumValue), $"Undefined enum (for `{typeof(TEnum)}`): {enumValue}");
	}

	[Conditional("DEBUG")]
	public static void DAssert_NotDefined<TEnum>(this TEnum enumValue) where TEnum : struct, Enum {
		Debug.Assert(!IsDefined(enumValue), $"Expecting undefined enum (for `{typeof(TEnum)}`) but instead got: {enumValue}");
	}
}
