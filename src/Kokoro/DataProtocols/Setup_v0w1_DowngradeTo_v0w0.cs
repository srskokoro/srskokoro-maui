namespace Kokoro.DataProtocols;
using Kokoro.Common.IO;
using Fs = Prot_v0w1.Fs;

internal static class Setup_v0w1_DowngradeTo_v0w0 {

	public static void Exec(KokoroContext ctx) {
		string dataPath = ctx.DataPath;
		FsUtils.DeleteDirectoryIfExists(Path.Join(dataPath, Fs.Extensions));
		FsUtils.DeleteDirectoryIfExists(Path.Join(dataPath, Fs.Media));
		FsUtils.DeleteDirectoryIfExists(Path.Join(dataPath, Fs.CollectionDbDir));
		FsUtils.DeleteDirectoryIfExists(Path.Join(dataPath, Fs.ConfigDbDir));
	}
}
