namespace Kokoro.Common.IO;
using Blake2Fast;
using System.IO.Enumeration;
using System.Runtime.InteropServices;

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
	public static void DeleteDirectoryAtomic(string path, ReadOnlySpan<char> trashDir) {
		string deleteLater = Path.Join(trashDir, Path.GetDirectoryName(path = Path.GetFullPath(path)));
		Debug.Assert(!File.Exists(path), $"Directory expected but is a file: {path}");

		try {
			Directory.Move(path, deleteLater);
			// ^ Will NOT throw if `path` isn't a directory
			// ^ Will throw if `trashDir` isn't a directory
			// ^ Will throw when moving to a different volume
		} catch (IOException) {
			// Check if we bumped into an existing file or directory, and if so,
			// delete it first instead.
			if (Directory.Exists(deleteLater)) {
				DeleteDirectory(deleteLater);
			} else if (File.Exists(deleteLater)) {
				// Bypass read-only attribute (as it would prevent deletion)
				File.SetAttributes(deleteLater, 0);
				File.Delete(deleteLater);
			} else {
				throw;
			}
		}

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

	// --

	// Crockford's Base 32 alphabet in lowercase
	private static ReadOnlySpan<byte> Base32EncodingMap => new byte[32] {
		// Relies on C# compiler optimization to reference static data
		// - See, https://github.com/dotnet/csharplang/issues/5295
		(byte)'0', (byte)'1', (byte)'2', (byte)'3', (byte)'4', (byte)'5', (byte)'6', (byte)'7',
		(byte)'8', (byte)'9', (byte)'a', (byte)'b', (byte)'c', (byte)'d', (byte)'e', (byte)'f',
		(byte)'g', (byte)'h', (byte)'j', (byte)'k', (byte)'m', (byte)'n', (byte)'p', (byte)'q',
		(byte)'r', (byte)'s', (byte)'t', (byte)'v', (byte)'w', (byte)'x', (byte)'y', (byte)'z',
	};

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static string GetRandomFileName(string seed)
		=> GetRandomFileName(MemoryMarshal.AsBytes<char>(seed));

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static string GetRandomFileName(ReadOnlySpan<char> seed)
		=> GetRandomFileName(MemoryMarshal.AsBytes(seed));

	[SkipLocalsInit]
	public static string GetRandomFileName(ReadOnlySpan<byte> seed) {
		// 8 bytes will provide 12 chars for the "8.3 filename" encoding below
		Span<byte> hash = stackalloc byte[8];
		Blake2b.ComputeAndWriteHash(8, seed, hash);

		// Equiv. to `string.Create<TState>(…)` without having to allocate a `SpanAction`
		ref char destRef = ref Strings.UnsafeCreate(12, out string filename);
		// ^ 12 is the length of "8.3 filenames" (a.k.a., "short filenames" or SFN)

		// The following is similar to `Path.Populate83FileNameFromRandomBytes()` used by `Path.GetRandomFileName()`
		// - See, https://github.com/dotnet/runtime/blob/v6.0.3/src/libraries/System.Private.CoreLib/src/System/IO/Path.cs#L801

		// Get references to avoid unnecessary range checking
		ref byte mapRef = ref MemoryMarshal.GetReference(Base32EncodingMap);
		ref byte srcRef = ref MemoryMarshal.GetReference(hash);

		// Consume the 5 LSBs of the first 5 bytes
		Unsafe.Add(ref destRef, 0) = (char)Unsafe.Add(ref mapRef, Unsafe.Add(ref srcRef, 0) & 0b_0001_1111);
		Unsafe.Add(ref destRef, 1) = (char)Unsafe.Add(ref mapRef, Unsafe.Add(ref srcRef, 1) & 0b_0001_1111);
		Unsafe.Add(ref destRef, 2) = (char)Unsafe.Add(ref mapRef, Unsafe.Add(ref srcRef, 2) & 0b_0001_1111);
		Unsafe.Add(ref destRef, 3) = (char)Unsafe.Add(ref mapRef, Unsafe.Add(ref srcRef, 3) & 0b_0001_1111);
		Unsafe.Add(ref destRef, 4) = (char)Unsafe.Add(ref mapRef, Unsafe.Add(ref srcRef, 4) & 0b_0001_1111);

		// Consume the 3 MSBs of bytes 0 and 1, and the 6th and 7th bit of bytes 3 and 4
		Unsafe.Add(ref destRef, 5) = (char)Unsafe.Add(ref mapRef
			, ((Unsafe.Add(ref srcRef, 0) & 0b_1110_0000) >> 5)
			| ((Unsafe.Add(ref srcRef, 3) & 0b_0110_0000) >> 2)
		);
		Unsafe.Add(ref destRef, 6) = (char)Unsafe.Add(ref mapRef
			, ((Unsafe.Add(ref srcRef, 1) & 0b_1110_0000) >> 5)
			| ((Unsafe.Add(ref srcRef, 4) & 0b_0110_0000) >> 2)
		);

		// Consume the 3 MSBs of byte 2, and the 8th bit from bytes 3 and 4
		int b32 = Unsafe.Add(ref srcRef, 2) >> 5;
		Debug.Assert((b32 & 0b_1111_1000) == 0, "Impossible!");
		b32 |= (Unsafe.Add(ref srcRef, 3) & 0b_1000_0000) >> 4;
		b32 |= (Unsafe.Add(ref srcRef, 4) & 0b_1000_0000) >> 3;
		Unsafe.Add(ref destRef, 7) = (char)Unsafe.Add(ref mapRef, b32);

		// Set the file extension separator
		Unsafe.Add(ref destRef, 8) = '.';

		// Consume the 5 LSBs of the remaining 3 bytes
		Unsafe.Add(ref destRef, 9)  = (char)Unsafe.Add(ref mapRef, Unsafe.Add(ref srcRef, 5) & 0b_0001_1111);
		Unsafe.Add(ref destRef, 10) = (char)Unsafe.Add(ref mapRef, Unsafe.Add(ref srcRef, 6) & 0b_0001_1111);
		Unsafe.Add(ref destRef, 11) = (char)Unsafe.Add(ref mapRef, Unsafe.Add(ref srcRef, 7) & 0b_0001_1111);

		return filename;
	}
}
