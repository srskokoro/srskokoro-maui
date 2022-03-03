namespace Kokoro.Internal.Util;

internal class CoreUtils {
#if DEBUG
	public const bool Debug = true;
#else
	public const bool Debug = false;
#endif
}
