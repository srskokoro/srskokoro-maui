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

	private readonly record struct CopyDirContents_FseResult(
		string Child, int RootLength, int NameLength, FileAttributes Attributes,
		(DateTimeOffset LastAccessUtc, DateTimeOffset LastWriteUtc) FileTimes
	);

	[SkipLocalsInit]
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
			string childDest = Path.Join(destDir, child.AsSpan(rootLen));

			FileSystemInfo fsInfo;
			if ((attr & FileAttributes.ReparsePoint) != 0) {
				string childDestDir = childDest.Substring(destDir.Length);
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
				string childDestDir = childDest.Substring(destDir.Length);
				Directory.CreateDirectory(childDestDir);
				File.Copy(child, childDest, overwrite: true);
				// ^- Copies timestamps & permission attributes automatically
				// - See, https://github.com/dotnet/runtime/issues/16366
				continue;
			}

		RestoreMetadataLikeFileCopy:
			// Make it behave like `File.Copy` which copies file metadata as
			// well. See, https://github.com/dotnet/runtime/issues/16366

			// Mimics, https://github.com/dotnet/runtime/blob/aded3141e39eb9391fb5dfbe367b866bfb26736a/src/native/libs/System.Native/pal_io.c#L1306
			// - See also, https://github.com/dotnet/corefx/pull/6098
			fsInfo.LastAccessTimeUtc = lastAccessUtc.DateTime;
			fsInfo.LastWriteTimeUtc = lastWriteUtc.DateTime;
			fsInfo.Attributes = attr;
			// TODO Copy permissions metadata as well?
			// - There's `FileSystemAclExtensions.SetAccessControl()` and
			// others but it seems Windows-specific though.

			// NOTE: It's not possible to change the compression status using
			// `File.SetAttributes()` or `FileSystemInfo.Attributes`.
			// - See also, https://stackoverflow.com/q/31032834
		}
	}

	public static void CopyDirectory(string srcDir, string destDir) {
		var srcDirInfo = new DirectoryInfo(srcDir);
		if (!srcDirInfo.Exists) throw new DirectoryNotFoundException(srcDirInfo.FullName);

		// NOTE: We don't throw when the destination already exists. We simply
		// merge contents and change metadata to match the source. This is
		// important when trying to recover from a crash and to resume an
		// interrupted copy operation.
		var destDirInfo = Directory.CreateDirectory(destDir);

		// Make it behave like `File.Copy` which copies file metadata as well.
		// See, https://github.com/dotnet/runtime/issues/16366

		// Mimics, https://github.com/dotnet/runtime/blob/aded3141e39eb9391fb5dfbe367b866bfb26736a/src/native/libs/System.Native/pal_io.c#L1306
		// - See also, https://github.com/dotnet/corefx/pull/6098
		destDirInfo.LastAccessTimeUtc = srcDirInfo.LastAccessTimeUtc;
		destDirInfo.LastWriteTimeUtc = srcDirInfo.LastWriteTimeUtc;
		destDirInfo.Attributes = srcDirInfo.Attributes;
		// TODO Copy permissions metadata as well?
		// - There's `FileSystemAclExtensions.SetAccessControl()` and others
		// but it seems Windows-specific though.

		CopyDirContents(srcDir, destDirInfo.FullName);
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
	/// Deletes the specified directory recursively, atomically, by first
	/// moving the specified directory inside the given trash directory. If a
	/// directory or file with the same name already exists under the trash
	/// directory, it is deleted first.
	/// </summary>
	[SkipLocalsInit]
	public static void DeleteDirectoryAtomic(string path, ReadOnlySpan<char> trashDir) {
		Debug.Assert(!File.Exists(path), $"Directory expected but is a file: {path}");
		string deleteLater = ForceTrash(path, trashDir);
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
	[SkipLocalsInit]
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
	/// Performs <see cref="DeleteDirectory(string)"/> if the given path is an
	/// <see cref="Directory.Exists(string?)">existing directory</see>.
	/// </summary>
	public static bool DeleteDirectoryIfExists(string path) {
		if (Directory.Exists(path)) {
			DeleteDirectory(path);
			return true;
		}
		return false;
	}

	/// <summary>
	/// Performs <see cref="DeleteDirectoryAtomic(string, ReadOnlySpan{char})"/>
	/// if the given path is an <see cref="Directory.Exists(string?)">existing
	/// directory</see>.
	/// </summary>
	public static bool DeleteDirectoryAtomicIfExists(string path, ReadOnlySpan<char> trashDir) {
		if (Directory.Exists(path)) {
			DeleteDirectoryAtomic(path, trashDir);
			return true;
		}
		return false;
	}

	/// <summary>
	/// Performs <see cref="ClearDirectory(string)"/> if the given path is an
	/// <see cref="Directory.Exists(string?)">existing directory</see>.
	/// </summary>
	public static bool ClearDirectoryIfExists(string path) {
		if (Directory.Exists(path)) {
			ClearDirectory(path);
			return true;
		}
		return false;
	}

	/// <summary>
	/// Renames or moves a file or a directory, to a path on the same volume.
	/// </summary>
	/// <remarks>
	/// If <paramref name="src"/> is a file, it will overwrite <paramref name="dest"/>
	/// if it's also a file.
	/// </remarks>
	public static void Rename(string src, string dest) {
		// Specification:
		// - Both the source and destination can be directories. The code must
		// be able to handle that case.
		// - When moving to a different volume, the code must throw.

		if (File.Exists(dest)) {
			// NOTE: The destination is a file. The source should be a file too,
			// or else, throw.
			File.Replace(src, dest, null);
			// ^ Will throw when moving to a different volume
		} else {
			// NOTE: The destination doesn't exist as a file. It might exist as
			// a directory though.
			Directory.Move(src, dest);
			// ^ Will throw when moving to a different volume -- unlike `File.Move()`
		}

		// See also,
		// - https://stackoverflow.com/questions/19875329/can-using-fileshare-delete-cause-a-unauthorizedaccessexception
		// - https://stackoverflow.com/questions/8958094/reliable-file-saving-file-replace-in-a-busy-environment
	}

	/// <summary>
	/// Deletes the target file or directory, atomically, by first moving the
	/// target inside the given trash directory. If a directory or file with the
	/// same name already exists under the trash directory, it is deleted first.
	/// </summary>
	/// <remarks>
	/// <para>
	/// If the specified target is a directory, it is deleted recursively.
	/// </para>
	/// <para>
	///	If the specified target does not exist, no exception is thrown (<see langword="false"/>
	///	is returned instead).
	/// </para>
	/// </remarks>
	/// <returns>
	/// Whether or not the target exists and was deleted successfully.
	/// </returns>
	[SkipLocalsInit]
	public static bool DeleteAtomic(string path, ReadOnlySpan<char> trashDir) {
		if (!Directory.Exists(path)) {
			if (!File.Exists(path)) goto NotExists; // Reduces indention
			string deleteLater = ForceTrash(path, trashDir);
			// Bypass read-only attribute (as it would prevent deletion)
			// - Side effect: also clears other file attributes.
			File.SetAttributes(deleteLater, 0);
			File.Delete(deleteLater);
			return true;
		} else {
			DeleteDirectoryAtomic(path, trashDir);
			return true;
		}
	NotExists:
		return false;
	}

	/// <summary>
	/// Moves the target file or directory to be inside the given trash
	/// directory. If a directory or file with the same name already exists
	/// under the trash directory, it is deleted first.
	/// </summary>
	[SkipLocalsInit]
	private static string ForceTrash(string path, ReadOnlySpan<char> trashDir) {
		string trashPath = Path.Join(trashDir, Path.GetDirectoryName(path = Path.GetFullPath(path)));
		try {
			Directory.Move(path, trashPath);
			// ^ Will NOT throw if `path` isn't a directory
			// ^ Will throw if `trashDir` isn't a directory
			// ^ Will throw when moving to a different volume
		} catch (IOException) {
			// Check if we bumped into an existing file or directory, and if so,
			// delete it first instead.
			if (Directory.Exists(trashPath)) {
				DeleteDirectory(trashPath);
			} else if (File.Exists(trashPath)) {
				// Bypass read-only attribute (as it would prevent deletion)
				File.SetAttributes(trashPath, 0);
				File.Delete(trashPath);
			} else {
				throw;
			}

			// Now, try again
			Directory.Move(path, trashPath);
		}
		return trashPath;
	}
}
