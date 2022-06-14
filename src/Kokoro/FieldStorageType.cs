global using FieldStorageTypeInt = System.Int32;

namespace Kokoro;

public enum FieldStorageType : FieldStorageTypeInt {
	Shared = 0,
	Hot    = 1,
	Cold   = 2,
}
