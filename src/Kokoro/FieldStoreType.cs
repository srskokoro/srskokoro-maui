global using FieldStoreTypeInt = System.Byte;
global using FieldStoreTypeUInt = System.Byte;
global using FieldStoreTypeSInt = System.SByte;

namespace Kokoro;

public enum FieldStoreType : FieldStoreTypeInt {
	Shared = 0,
	Hot    = 1,
	Cold   = 2,
}

public static class FieldStoreTypeExtensions {

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsValid(this FieldStoreType @enum) {
		// Ternary operator returning true/false prevents redundant asm generation:
		// See, https://github.com/dotnet/runtime/issues/4207#issuecomment-147184273
		return (FieldStoreTypeUInt)@enum > (FieldStoreTypeUInt)FieldStoreType.Cold ? false : true;
	}
}
