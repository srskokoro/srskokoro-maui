namespace Kokoro.Internal;
using Blake2Fast.Implementation;
using Kokoro.Common.IO;
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
	public void WriteTo(Stream destination) {
		int length = _Length;
		if (length > 0) {
			var source = _Stream;
			if (source == null) {
				// Pretend we're using `Stream.Null`
				goto E_EndOfStreamRead_InvOp;
			}
			source.Position = _Offset;
			if (source.CopyPartlyTo(destination, length) != 0) {
				// Not enough data read to reach the supposed length
				goto E_EndOfStreamRead_InvOp;
			}
		}
		return;

	E_EndOfStreamRead_InvOp:
		StreamUtils.E_EndOfStreamRead_InvOp();
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void FeedTo(ref Blake2bHashState hasher) {
		int length = _Length;
		if (length > 0) {
			hasher.UpdateLE((uint)length); // i.e., length-prepended

			var source = _Stream;
			if (source == null) {
				// Pretend we're using `Stream.Null`
				goto E_EndOfStreamRead_InvOp;
			}

			source.Position = _Offset;
			if (source.FeedPartlyTo(ref hasher, length) != 0) {
				// Not enough data read to reach the supposed length
				goto E_EndOfStreamRead_InvOp;
			}
			return;
		}
		hasher.UpdateLE((uint)0); // i.e., zero length
		return;

	E_EndOfStreamRead_InvOp:
		StreamUtils.E_EndOfStreamRead_InvOp();
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
