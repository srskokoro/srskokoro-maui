namespace Kokoro.Internal;
using Blake2Fast.Implementation;
using System.IO;

internal readonly record struct LatentFieldVal {

	public static LatentFieldVal Null => NullInstHolder.Instance;

	private static class NullInstHolder {
		internal static readonly LatentFieldVal Instance = new(Stream.Null, 0, 0);
	}

	private readonly Stream? _Stream;
	private readonly int _Offset;
	private readonly int _Length;

	public readonly Stream Stream {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get {
			var _ = _Stream;
			if (_ != null) return _;
			return Stream.Null;
		}
	}

	/// <remarks>
	/// WARNING: Bogus and noncompliant data sources may cause this to have a
	/// negative value.
	/// </remarks>
	public readonly int Offset => _Offset;

	/// <remarks>
	/// WARNING: Bogus and noncompliant data sources may cause this to have a
	/// negative value.
	/// </remarks>
	public readonly int Length => _Length;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public LatentFieldVal(Stream Stream, int Offset, int Length) {
		_Stream = Stream;
		_Offset = Offset;
		_Length = Length;
	}

	/// <param name="Stream">Will contain the same value as <see cref="Stream"/></param>
	/// <param name="Offset">Will contain the same value as <see cref="Offset"/></param>
	/// <param name="Length">Will contain the same value as <see cref="Length"/></param>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly void Deconstruct(out Stream Stream, out int Offset, out int Length) {
		Stream = this.Stream;
		Offset = _Offset;
		Length = _Length;
	}

	// --

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[SkipLocalsInit]
	public void WriteTo(Stream destination) {
		int remaining = _Length;
		if (remaining > 0) {
			var source = _Stream;
			if (source == null) goto ZeroFillRemaining;

			source.Position = _Offset;
			remaining = source.CopyPartlyTo(destination, remaining);
			if (remaining != 0) goto ZeroFillRemaining;
		}
		return;

	ZeroFillRemaining:
		destination.ClearPartly(remaining);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[SkipLocalsInit]
	public void FeedTo(ref Blake2bHashState hasher) {
		int remaining = _Length;
		if (remaining > 0) {
			hasher.UpdateLE((uint)remaining); // i.e., length-prepended

			var source = _Stream;
			if (source == null) goto ZeroFillRemaining;

			source.Position = _Offset;
			remaining = source.FeedPartlyTo(ref hasher, remaining);
			if (remaining != 0) goto ZeroFillRemaining;
			return;
		}
		hasher.UpdateLE((uint)0); // i.e., zero length
		return;

	ZeroFillRemaining:
		FeedWithNullBytes(ref hasher, remaining);

		// Never inline, as this is expected to be an uncommon path
		[MethodImpl(MethodImplOptions.NoInlining)]
		static void FeedWithNullBytes(ref Blake2bHashState hasher, int count) {
			while (--count >= 0)
				hasher.Update<byte>(0);
		}
	}

	// --

	#region Equality and Comparability

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly bool Equals(LatentFieldVal other) {
		if (_Stream == other._Stream && _Offset == other._Offset && _Length == other._Length)
			return true;
		return false;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public override readonly int GetHashCode()
		=> HashCode.Combine(_Stream, _Offset, _Length);

	#endregion
}
