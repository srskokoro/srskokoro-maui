namespace Kokoro.App.Util;

internal class AppUtil {
#if DEBUG
	public const bool Debug = true;
#else
	public const bool Debug = false;
#endif
}
