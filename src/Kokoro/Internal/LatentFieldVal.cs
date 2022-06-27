﻿namespace Kokoro.Internal;
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

	public readonly int Offset => _Offset;
	public readonly int Length => _Length;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public LatentFieldVal(Stream Stream, int Offset, int Length) {
		_Stream = Stream;
		_Offset = Offset;
		_Length = Length;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly void Deconstruct(out Stream Stream, out int Offset, out int Length) {
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
