namespace Kokoro;

public readonly struct KokoroDataVersion : IEquatable<KokoroDataVersion>, IComparable, IComparable<KokoroDataVersion> {
	private readonly ulong _VersionBits;
	private const int VersionBits_MajorShift = 32;

	/// <summary>The major schema version.</summary>
	public uint Major => (uint)(_VersionBits >> VersionBits_MajorShift);

	/// <summary>The minor schema version.</summary>
	public uint Minor => (uint)_VersionBits;

	private KokoroDataVersion(ulong versionBits)
		=> _VersionBits = versionBits;

	public KokoroDataVersion(uint major, uint minor)
		=> _VersionBits = (ulong)major << VersionBits_MajorShift | minor;

	public KokoroDataVersion(ReadOnlySpan<char> major, ReadOnlySpan<char> minor)
		: this(ParseMajorVersion(major), ParseMinorVersion(minor)) { }

	private const uint LibVersionBits_Major = 0;
	private const uint LibVersionBits_Minor = 1;
	private const ulong LibVersionBits =
		(ulong)LibVersionBits_Major << VersionBits_MajorShift | LibVersionBits_Minor;

	public static KokoroDataVersion LibVersion => new(LibVersionBits);

	public bool SameAsLib {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get {
			// Ternary operator returning true/false prevents redundant asm generation:
			// See, https://github.com/dotnet/runtime/issues/4207#issuecomment-147184273
			return _VersionBits == LibVersionBits ? true : false;
		}
	}

	public bool Operable {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get {
			// Ternary operator returning true/false prevents redundant asm generation:
			// See, https://github.com/dotnet/runtime/issues/4207#issuecomment-147184273
			return Major == LibVersionBits_Major && Minor >= LibVersionBits_Minor ? true : false;
		}
	}

	public static KokoroDataVersion Zero => default;

	internal const string ZeroString = "0.0";
	public override string ToString() => $"{Major}.{Minor}";

	public static KokoroDataVersion Parse(ReadOnlySpan<char> s) {
		int dotIndex = s.IndexOf('.');
		if (dotIndex < 0) {
			uint major = ParseMajorVersion(s);
			return new(major, 0);
		} else {
			uint major = ParseMajorVersion(s[..dotIndex]);
			uint minor = ParseMinorVersion(s[(dotIndex+1)..]);
			return new(major, minor);
		}
	}

	[SkipLocalsInit]
	private static uint ParseMajorVersion(ReadOnlySpan<char> s) {
		try {
			return uint.Parse(s);
		} catch (Exception ex) {
			throw new FormatException($"Invalid major schema version: {s}", ex);
		}
	}

	[SkipLocalsInit]
	private static uint ParseMinorVersion(ReadOnlySpan<char> s) {
		try {
			return uint.Parse(s);
		} catch (Exception ex) {
			throw new FormatException($"Invalid minor schema version: {s}", ex);
		}
	}

	#region Equality and Comparability

	public override bool Equals([NotNullWhen(true)] object? obj) {
		// Ternary operator returning true/false prevents redundant asm generation:
		// See, https://github.com/dotnet/runtime/issues/4207#issuecomment-147184273
		return obj is KokoroDataVersion other && Equals(other) ? true : false;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool Equals(KokoroDataVersion other) {
		// Ternary operator returning true/false prevents redundant asm generation:
		// See, https://github.com/dotnet/runtime/issues/4207#issuecomment-147184273
		return _VersionBits == other._VersionBits ? true : false;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public override int GetHashCode() => _VersionBits.GetHashCode();

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public int CompareTo(object? obj) {
		if (obj is KokoroDataVersion other) return CompareTo(other);
		if (obj != null) CompareTo__E_IncompatibleType_Arg();
		return 1;
	}

	[DoesNotReturn]
	private static void CompareTo__E_IncompatibleType_Arg()
		=> throw new ArgumentException($"Object must be of type {nameof(KokoroDataVersion)}.");

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public int CompareTo(KokoroDataVersion other) => _VersionBits.CompareTo(other._VersionBits);

	#region Relational Operators

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool operator ==(KokoroDataVersion left, KokoroDataVersion right) {
		// Ternary operator returning true/false prevents redundant asm generation:
		// See, https://github.com/dotnet/runtime/issues/4207#issuecomment-147184273
		return left._VersionBits == right._VersionBits ? true : false;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool operator !=(KokoroDataVersion left, KokoroDataVersion right) {
		// Ternary operator returning true/false prevents redundant asm generation:
		// See, https://github.com/dotnet/runtime/issues/4207#issuecomment-147184273
		return left._VersionBits != right._VersionBits ? true : false;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool operator <(KokoroDataVersion left, KokoroDataVersion right) {
		// Ternary operator returning true/false prevents redundant asm generation:
		// See, https://github.com/dotnet/runtime/issues/4207#issuecomment-147184273
		return left._VersionBits < right._VersionBits ? true : false;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool operator <=(KokoroDataVersion left, KokoroDataVersion right) {
		// Ternary operator returning true/false prevents redundant asm generation:
		// See, https://github.com/dotnet/runtime/issues/4207#issuecomment-147184273
		return left._VersionBits <= right._VersionBits ? true : false;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool operator >(KokoroDataVersion left, KokoroDataVersion right) {
		// Ternary operator returning true/false prevents redundant asm generation:
		// See, https://github.com/dotnet/runtime/issues/4207#issuecomment-147184273
		return left._VersionBits > right._VersionBits ? true : false;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool operator >=(KokoroDataVersion left, KokoroDataVersion right) {
		// Ternary operator returning true/false prevents redundant asm generation:
		// See, https://github.com/dotnet/runtime/issues/4207#issuecomment-147184273
		return left._VersionBits >= right._VersionBits ? true : false;
	}

	#endregion

	#endregion
}
