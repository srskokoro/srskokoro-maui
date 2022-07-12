namespace Kokoro.Internal;
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
		var source = _Stream;
		if (source != null) {
			source.Position = _Offset;
			source.CopyPartlyTo(destination, _Length);
		}
	}

	// --

	#region Equality and Comparability

	public readonly bool Equals(LatentFieldVal other) {
		if (_Stream == other._Stream && _Offset == other._Offset && _Length == other._Length)
			return true;
		return false;
	}

	public override readonly int GetHashCode()
		=> HashCode.Combine(_Stream, _Offset, _Length);

	#endregion
}
