namespace Kokoro.Util;

#pragma warning disable CA1069 // Enums values should not be duplicated
[Flags]
internal enum DisposeState : uint {
	None                   = 0,

	DisposedPartially_Flag = 1,
	DisposedPartially      = 1|None,

	Disposing_Flag         = 2,
	Disposing              = 2|DisposedPartially,

	DisposedFully_Flag     = 4,
	DisposedFully          = 4|Disposing,
}
#pragma warning restore CA1069 // Enums values should not be duplicated
