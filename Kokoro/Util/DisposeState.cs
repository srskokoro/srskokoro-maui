namespace Kokoro.Util;

#pragma warning disable CA1069 // Enums values should not be duplicated
[Flags]
internal enum DisposeState : uint {
	None                   = 0,

	DisposedPartially      = 1|None,
	DisposedPartially_Flag = 1,

	Disposing              = 2|DisposedPartially,
	Disposing_Flag         = 2,

	DisposedFully          = 4|Disposing,
	DisposedFully_Flag     = 4,
}
#pragma warning restore CA1069 // Enums values should not be duplicated
