﻿global using FieldStoreTypeInt = System.Int32;

namespace Kokoro;

public enum FieldStoreType : FieldStoreTypeInt {
	Shared = 0,
	Hot    = 1,
	Cold   = 2,
}
