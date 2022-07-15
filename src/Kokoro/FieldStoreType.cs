global using FieldStoreTypeInt = System.UInt32;
global using FieldStoreTypeUInt = System.UInt32;
global using FieldStoreTypeSInt = System.Int32;

namespace Kokoro;

public enum FieldStoreType : FieldStoreTypeInt {
	Shared = 0,
	Hot    = 1,
	Cold   = 2,
}
