namespace Kokoro.Common.IO;

using System.IO.Enumeration;

/// <summary>
/// Some file system utilities.
/// </summary>
internal static class FsUtils {

	internal static class EnumerationOptionsHolder {
		internal static readonly EnumerationOptions Default =
			new() { MatchType = MatchType.Simple, AttributesToSkip = 0, IgnoreInaccessible = false };

		internal static readonly EnumerationOptions DefaultRecursive =
			new() { MatchType = MatchType.Simple, AttributesToSkip = 0, IgnoreInaccessible = false, RecurseSubdirectories = true };
	}

	public static void CopyDirectory(string srcDir, string destDir)
		=> CopyDirContents(srcDir, Directory.CreateDirectory(destDir).FullName);

	private readonly record struct CopyDirContents_FseResult(
		string Child, int RootLength, int NameLength, FileAttributes Attributes,
		(DateTimeOffset LastAccessUtc, DateTimeOffset LastWriteUtc) FileTimes
	) { }

	public static void CopyDirContents(string srcDir, string destDir) {
		// Will throw (on enumeration) if not a directory
		FileSystemEnumerable<CopyDirContents_FseResult> fse = new(srcDir,
			static (ref FileSystemEntry entry) => {
				var attr = entry.Attributes;
				return new(
					entry.ToFullPath(),
					entry.RootDirectory.Length,
					entry.FileName.Length,
					attr,
					((attr & (FileAttributes.Directory|FileAttributes.ReparsePoint)) == 0)
						? default : (entry.LastAccessTimeUtc, entry.LastWriteTimeUtc)
				);
			},
			EnumerationOptionsHolder.DefaultRecursive
		) {
			ShouldRecursePredicate = static (ref FileSystemEntry entry)
				=> (entry.Attributes & FileAttributes.ReparsePoint) == 0,
		};

		foreach (var (child, rootLen, nameLen, attr, (lastAccessUtc, lastWriteUtc)) in fse) {
			string childDest = Path.Join(destDir, child.AsSpan()[rootLen..]);

			FileSystemInfo fsInfo;
			if ((attr & FileAttributes.ReparsePoint) != 0) {
				string childDestDir = childDest[destDir.Length..];
				Directory.CreateDirectory(childDestDir);
				if ((attr & FileAttributes.Directory) != 0) {
					if (new DirectoryInfo(child).LinkTarget is string linkTarget) {
						fsInfo = Directory.CreateSymbolicLink(childDest, linkTarget);
						goto RestoreMetadataLikeFileCopy;
					}
				} else {
					if (new FileInfo(child).LinkTarget is string linkTarget) {
						fsInfo = File.CreateSymbolicLink(childDest, linkTarget);
						goto RestoreMetadataLikeFileCopy;
					}
				}
				continue; // Skip
			} else if ((attr & FileAttributes.Directory) != 0) {
				fsInfo = Directory.CreateDirectory(childDest);
				goto RestoreMetadataLikeFileCopy;
			} else {
				string childDestDir = childDest[destDir.Length..];
				Directory.CreateDirectory(childDestDir);
				File.Copy(child, childDest, overwrite: true);
				// ^- Copies timestamps & permission attributes automatically
				// - See, https://github.com/dotnet/runtime/issues/16366
				continue;
			}

		RestoreMetadataLikeFileCopy:
			// Make the operation behave like `File.Copy` which copies
			// timestamps & permission attributes automatically.
			// - See, https://github.com/dotnet/runtime/issues/16366

			// Mimics, https://github.com/dotnet/runtime/blob/aded3141e39eb9391fb5dfbe367b866bfb26736a/src/native/libs/System.Native/pal_io.c#L1306
			// - See also, https://github.com/dotnet/corefx/pull/6098
			fsInfo.LastAccessTimeUtc = lastAccessUtc.DateTime;
			fsInfo.LastWriteTimeUtc = lastWriteUtc.DateTime;
			fsInfo.Attributes = attr;
			// Not copying permissions because we don't know how to do it in a
			// portable way. As a bonus, we get to avoid the extra overhead.

			// NOTE: It's not possible to change the compression status using
			// `File.SetAttributes()` or `FileSystemInfo.Attributes`.
			// - See also, https://stackoverflow.com/q/31032834
		}
	}

	/// <summary>
	/// Deletes the specified directory recursively.
	/// </summary>
	/// <remarks>
	/// Unlike <see cref="Directory.Delete(string, bool)"/>, this clears the
	/// <see cref="FileAttributes.ReadOnly">read-only attribute</see> of each
	/// file/directory being deleted, in order to bypass 'access denied' errors.
	/// </remarks>
	public static void DeleteDirectory(string path) {
		ClearDirectory(path); // Will throw if not a directory

		// Bypass read-only attribute (as it would prevent deletion)
		File.SetAttributes(path, 0);
		Directory.Delete(path);
	}

	/// <summary>
	/// Deletes the specified directory recursively, atomically.
	/// </summary>
	/// <remarks>
	/// Works like <see cref="DeleteDirectory(string)"/> but renames the
	/// target first (with a random name) before attempting deletion.
	/// </remarks>
	public static void DeleteDirectoryAtomic(string path) {
		Debug.Assert(!File.Exists(path), $"Directory expected but is a file: {path}");

		// TODO Hash instead the filename, generate an 8.3 filename from it,
		// and if the resulting path already exists, delete it first, then
		// finally, rename the target path to that to delete it.
		path = Path.GetFullPath(path);
		var dir = Path.GetDirectoryName(path.AsSpan());
		string deleteLater = Path.Join(dir, Path.GetRandomFileName());

		Directory.Move(path, deleteLater); // Will NOT throw if not a directory
		DeleteDirectory(deleteLater); // Will throw if not a directory
	}

	/// <summary>
	/// Deletes the specified directory recursively, atomically.
	/// </summary>
	/// <remarks>
	/// Works like <see cref="DeleteDirectoryAtomic(string)"/> but moves the
	/// target first to the specified <paramref name="trashDir"/> directory
	/// before attempting deletion.
	/// </remarks>
	public static void DeleteDirectoryAtomic(string path, string trashDir) {
		Debug.Assert(!File.Exists(path), $"Directory expected but is a file: {path}");

		// TODO Hash instead `path`, generate an 8.3 filename from it, and if
		// the resulting path already exists, delete it first, then finally,
		// rename the `path` to that to delete it later.
		string deleteLater = Path.Join(trashDir, Path.GetRandomFileName());

		Directory.Move(path, deleteLater);
		// ^ Will NOT throw if `path` isn't a directory
		// ^ Will throw if `trashDir` isn't a directory
		// ^ Will throw when moving to a different volume

		DeleteDirectory(deleteLater);
	}

	/// <summary>
	/// Deletes directory contents recursively until the directory is empty.
	/// </summary>
	/// <remarks>
	/// Unlike <see cref="Directory.Delete(string, bool)"/>, the directory
	/// itself is not deleted. Also, this clears the <see cref="FileAttributes.ReadOnly">read-only
	/// attribute</see> of each file/directory being deleted, in order to
	/// bypass 'access denied' errors.
	/// </remarks>
	public static void ClearDirectory(string path) {
		// Will throw (on enumeration) if not a directory
		FileSystemEnumerable<(string, FileAttributes)> fse = new(path
			, static (ref FileSystemEntry entry) => (entry.ToFullPath(), entry.Attributes)
			, EnumerationOptionsHolder.Default);

		foreach (var (child, attr) in fse) {
			// Bypass read-only attribute (as it would prevent deletion)
			if ((attr & FileAttributes.ReadOnly) != 0) {
				File.SetAttributes(child, attr ^ FileAttributes.ReadOnly);
			}
			if ((attr & FileAttributes.Directory) != 0) {
				// Avoids recursing directory symlinks
				if ((attr & FileAttributes.ReparsePoint) == 0) {
					ClearDirectory(child);
				}
				Directory.Delete(child);
			} else {
				File.Delete(child);
			}
		}
	}

	/// <summary>
	/// Moves/renames the specified directory, deleting the destination
	/// directory if it already exists.
	/// </summary>
	public static void ForceMoveDirectory(string path, string dest) {
		Debug.Assert(!File.Exists(path), $"Directory expected but is a file: {path}");

		string? deleteLater = null;
		// Before we rename the `dest` directory (for the purposes of deletion),
		// we make sure that the source `path` really is a directory; Otherwise,
		// we don't perform the rename and just let `Directory.Move(path, dest)`
		// to throw for us later down.
		if (Directory.Exists(dest) && Directory.Exists(path)) {
			// TODO Hash instead the filename, generate an 8.3 filename from it,
			// and if the resulting path already exists, delete it first, then
			// finally, rename the target path to that to delete it later.
			deleteLater = Path.GetRandomFileName();
			// Push the existing directory out of the way
			Directory.Move(dest, deleteLater);
		}

		// If `dest` doesn't exist, the following will NOT throw if `path`
		// isn't a directory.
		Directory.Move(path, dest);

		// Delete the directory we pushed away
		if (deleteLater != null)
			DeleteDirectory(deleteLater); // Will throw if not a directory
	}
}
