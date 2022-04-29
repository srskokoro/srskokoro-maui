namespace Kokoro.Common.Sqlite;
using Kokoro.Common.IO;
using System.Buffers;
using System.Runtime.InteropServices;

internal static class SqliteUtils {

	/// <summary>
	/// Deletes the sqlite database along with its journal/WAL files. This will
	/// delete such files, even if the database file itself no longer exists.
	/// </summary>
	/// <remarks>
	/// Also useful when avoiding a dangerous scenario where the journal/WAL
	/// file could end up being mispaired against a different/new database.
	/// <para>
	/// See, “<see href="https://www.sqlite.org/howtocorrupt.html#_unlinking_or_renaming_a_database_file_while_in_use">2.4.
	/// Unlinking or renaming a database file while in use | How To Corrupt An
	/// SQLite Database File | sqlite.org</see>”
	/// </para>
	/// </remarks>
	public static void DeleteSqliteDb(string path) {
		File.Delete(path);
		File.Delete($"{path}-wal");
		File.Delete($"{path}-shm");
		File.Delete($"{path}-journal");

		// Also delete files for when SQLite is using 8.3 filenames
		// - See, https://www.sqlite.org/shortnames.html
		{
			var pathSpan = path.AsSpan();
			var extLen = Path.GetExtension(pathSpan).Length; // NOTE: Includes '.'
			if (extLen is > 1 and <= 4) {
				if (Path.GetFileNameWithoutExtension(pathSpan).Length <= 8) {
					var pathNoExt = pathSpan[..^extLen];
					File.Delete($"{pathNoExt}.wal");
					File.Delete($"{pathNoExt}.shm");
					File.Delete($"{pathNoExt}.nal");
				}
			}
		}

		// Don't know how to delete super-journal files, so just end here.
		//
		// Still, it should be safe to not delete super-journal files. Quote
		// from, “5.0 Writing to a database file | File Locking And Concurrency
		// In SQLite Version 3”:
		//
		// > 5. … The name of the super-journal is arbitrary. (The current
		// > implementation appends random suffixes to the name of the main
		// > database file until it finds a name that does not previously
		// exist.) …
		//
		// See, https://web.archive.org/web/20220407150600/https://sqlite.org/lockingv3.html#writing:~:text=The%20current%20implementation%20appends,exist%2E
	}

	// --

	private const bool _1 = true;
	private const bool __ = false;

	// `true` for all RFC 3986 unreserved and reserved characters except '?' and '#'
	private static ReadOnlySpan<bool> UriFilenameTable => new bool[256] {
		// Relies on C# compiler optimization to reference static data
		// - See, https://github.com/dotnet/csharplang/issues/5295

		__/*NUL*/,__/*SOH*/,__/*STX*/,__/*ETX*/,__/*EOT*/,__/*ENQ*/,__/*ACK*/,__/*BEL*/,__/*BS*/, __/*HT*/, __/*LF*/, __/*VT*/, __/*FF*/, __/*CR*/, __/*SO*/, __/*SI*/,
		__/*DLE*/,__/*DC1*/,__/*DC2*/,__/*DC3*/,__/*DC4*/,__/*NAK*/,__/*SYN*/,__/*ETB*/,__/*CAN*/,__/*EM*/, __/*SUB*/,__/*ESC*/,__/*FS*/, __/*GS*/, __/*RS*/, __/*US*/,
		__/*SP*/, _1/*!*/,  __/*"*/,  __/*#*/,  _1/*$*/,  __/*%*/,  _1/*&*/,  _1/*'*/,  _1/*(*/,  _1/*)*/,  _1/***/,  _1/*+*/,  _1/*,*/,  _1/*-*/,  _1/*.*/,  _1/*/*/,
		_1/*0*/,  _1/*1*/,  _1/*2*/,  _1/*3*/,  _1/*4*/,  _1/*5*/,  _1/*6*/,  _1/*7*/,  _1/*8*/,  _1/*9*/,  _1/*:*/,  _1/*;*/,  __/*<*/,  _1/*=*/,  __/*>*/,  __/*?*/,
		_1/*@*/,  _1/*A*/,  _1/*B*/,  _1/*C*/,  _1/*D*/,  _1/*E*/,  _1/*F*/,  _1/*G*/,  _1/*H*/,  _1/*I*/,  _1/*J*/,  _1/*K*/,  _1/*L*/,  _1/*M*/,  _1/*N*/,  _1/*O*/,
		_1/*P*/,  _1/*Q*/,  _1/*R*/,  _1/*S*/,  _1/*T*/,  _1/*U*/,  _1/*V*/,  _1/*W*/,  _1/*X*/,  _1/*Y*/,  _1/*Z*/,  _1/*[*/,  __/*\*/,  _1/*]*/,  __/*^*/,  _1/*_*/,
		__/*`*/,  _1/*a*/,  _1/*b*/,  _1/*c*/,  _1/*d*/,  _1/*e*/,  _1/*f*/,  _1/*g*/,  _1/*h*/,  _1/*i*/,  _1/*j*/,  _1/*k*/,  _1/*l*/,  _1/*m*/,  _1/*n*/,  _1/*o*/,
		_1/*p*/,  _1/*q*/,  _1/*r*/,  _1/*s*/,  _1/*t*/,  _1/*u*/,  _1/*v*/,  _1/*w*/,  _1/*x*/,  _1/*y*/,  _1/*z*/,  __/*{*/,  __/*|*/,  __/*}*/,  _1/*~*/,  __/*DEL*/,

		__,__,__,__,__,__,__,__, __,__,__,__,__,__,__,__,
		__,__,__,__,__,__,__,__, __,__,__,__,__,__,__,__,
		__,__,__,__,__,__,__,__, __,__,__,__,__,__,__,__,
		__,__,__,__,__,__,__,__, __,__,__,__,__,__,__,__,
		__,__,__,__,__,__,__,__, __,__,__,__,__,__,__,__,
		__,__,__,__,__,__,__,__, __,__,__,__,__,__,__,__,
		__,__,__,__,__,__,__,__, __,__,__,__,__,__,__,__,
		__,__,__,__,__,__,__,__, __,__,__,__,__,__,__,__,
	};

	// TODO Move this to a more fitting place
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static void ToHexBytes(byte val, ref byte dest) {
		// From, https://github.com/dotnet/runtime/blob/v6.0.4/src/libraries/Common/src/System/HexConverter.cs#L71
		// - See, https://github.com/dotnet/runtime/blob/v6.0.4/src/libraries/Common/src/System/HexConverter.cs#L32
		//
		// See also,
		// - https://github.com/dotnet/runtime/pull/1273#issuecomment-570680919
		// - https://github.com/dotnet/runtime/pull/1273#issuecomment-578241836

		uint difference = ((val & 0xF0u) << 4) + (val & 0x0Fu) - 0x8989u;
		uint packedResult = (((uint)(-(int)difference) & 0x7070u) >> 4) + difference + 0xB9B9u;

		U.Add(ref dest, 1) = (byte)(char)packedResult;
		U.Add(ref dest, 0) = (byte)(char)(packedResult >> 8);
	}

	/// <summary>
	/// Converts the specified path to an SQLite URI. (See,
	/// “<see href="https://www.sqlite.org/uri.html">URI Filenames In SQLite |
	/// sqlite.org</see>”)
	/// </summary>
	/// <remarks>
	/// Although SQLite supports relative pathnames in the URI, the given path
	/// is first converted into an absolute path instead, in order to simplify
	/// the processing and underlying implementation.
	/// <para>
	/// Unlike <see cref="Uri"/> (at least as of writing), this utility
	/// correctly handles paths that contain percent signs and '#' characters.
	/// See also, <see href="https://stackoverflow.com/a/35734486"/>
	/// </para>
	/// </remarks>
	[SkipLocalsInit]
	public static string ToUriFilename(string path) {
		// Better check first rather than call `Path.GetFullPath()` directly as
		// that method will perform argument validation by iterating over every
		// character unnecessarily, even when the given path is already absolute
		// and valid, when we don't even require that the given path be valid.
		// (That's how `Path.GetFullPath()` works as of writing this.)
		if (!Path.IsPathFullyQualified(path)) path = Path.GetFullPath(path);

		const int FileSchemeLength = 5;
		const int ExtraSlashes = 3;

		int utf8Length = path.GetUTF8ByteCount();
		int bufferMax = FileSchemeLength + ExtraSlashes + utf8Length * 3;

		const int MaxStackalloc = 1024; // 1 KiB

		byte[]? rented = null;
		// NOTE: See, https://github.com/dotnet/runtime/issues/7307
		// - See also, https://github.com/dotnet/runtime/issues/24963
		Span<byte> buffer = bufferMax <= MaxStackalloc && RuntimeHelpers.TryEnsureSufficientExecutionStack()
			? stackalloc byte[bufferMax] : (rented = ArrayPool<byte>.Shared.Rent(bufferMax));

		// Get a reference to avoid unnecessary range checking
		ref byte b = ref MemoryMarshal.GetReference(buffer);

		// The "file:" scheme component
		int n = 0;
		U.Add(ref b, n++) = (byte)'f';
		U.Add(ref b, n++) = (byte)'i';
		U.Add(ref b, n++) = (byte)'l';
		U.Add(ref b, n++) = (byte)'e';
		U.Add(ref b, n++) = (byte)':';

		// The (empty) authority component
		U.Add(ref b, n++) = (byte)'/';
		U.Add(ref b, n++) = (byte)'/';

		// The beginning of the URI path component, representing the absolute
		// file path
		U.Add(ref b, n++) = (byte)'/';

		int i = buffer.Length - utf8Length;
		// ^ Right-aligns the resulting UTF-8 bytes, so that we can use the
		// buffer as both the source and destination: eventually, we'll take
		// each UTF-8 byte, transform as necessary, and place each to the
		// left-hand side of the buffer.
		path.GetUTF8Bytes(MemoryMarshal.CreateSpan(ref U.Add(ref b, i), utf8Length));

		// On Windows, if the path begins with a drive letter, a single '/'
		// character must be prepended, so that it would correctly represent an
		// absolute pathname in the SQLite URI. However, if we're instead not on
		// Windows and the given path is absolute, we're guaranteed to begin
		// with a '/' character; thus, we must skip that, as we've already added
		// the needed '/' character beforehand.
		if (!OperatingSystem.IsWindows()) {
			i++;
			Debug.Assert(path[0] == '/');
			Debug.Assert(U.Add(ref b, i-1) == '/');
			Debug.Assert(U.Add(ref b, n-1) == '/');
		}

		// Get a reference to avoid unnecessary range checking
		ref bool mapRef = ref MemoryMarshal.GetReference(UriFilenameTable);

		for (; i < buffer.Length; i++) {
			byte c = U.Add(ref b, i);

			if (U.Add(ref mapRef, c)) {
				U.Add(ref b, n++) = c;
				continue;
			}

			// - On Windows only, convert all '\' characters into '/'
			// - Convert all sequences of two or more '/' characters into a
			// single '/' character
			if (c == Path.DirectorySeparatorChar) {
				if (U.Add(ref b, n-1) != (byte)'/')
					U.Add(ref b, n++) = (byte)'/';
				continue;
			}

			U.Add(ref b, n++) = (byte)'%';
			ToHexBytes(c, ref U.Add(ref b, n));
			n += 2;
		}

		string uri = Encoding.UTF8.GetString(buffer[..n]);

		if (rented != null)
			ArrayPool<byte>.Shared.Return(rented);

		return uri;
	}
}
