namespace Kokoro.Internal;
using System.IO;

internal readonly record struct LatentFieldVal {

	public static LatentFieldVal Null => NullInstHolder.Instance;

	private static class NullInstHolder {
		internal static readonly LatentFieldVal Instance = new(Stream.Null, 0, 0);
	}

	private readonly Stream? _Stream;
	// TODO Use `int` offset and length -- given that SQLite doesn't support BLOBs > 2147483647 (i.e., `int.MaxValue`)
	private readonly long _Offset;
	private readonly long _Length;

	public readonly Stream Stream {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get {
			var _ = _Stream;
			if (_ != null) return _;
			return Stream.Null;
		}
	}

	public readonly long Offset => _Offset;
	public readonly long Length => _Length;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public LatentFieldVal(Stream Stream, long Offset, long Length) {
		_Stream = Stream;
		_Offset = Offset;
		_Length = Length;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly void Deconstruct(out Stream Stream, out long Offset, out long Length) {
		Stream = this.Stream;
		Offset = _Offset;
		Length = _Length;
	}

	// --

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void WriteTo(Stream destination) {
		var source = Stream;
		source.Position = _Offset;
		source.CopyPartlyTo(destination, _Length);
	}
}
