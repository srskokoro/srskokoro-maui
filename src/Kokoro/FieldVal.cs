﻿namespace Kokoro;
using Kokoro.Common.Util;

public sealed class FieldVal {

	public static FieldVal Null => NullInstHolder.Instance;

	private static class NullInstHolder {
		internal static readonly FieldVal Instance = new();
	}

	private readonly FieldTypeHint _TypeHint;
	private readonly byte[] _Data;

	public FieldTypeHint TypeHint => _TypeHint;

	public ReadOnlySpan<byte> Data {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => _Data;
	}

	public FieldVal() {
		_TypeHint = FieldTypeHint.Null;
		_Data = Array.Empty<byte>();
	}

	public FieldVal(FieldTypeHintInt typeHint, byte[] data) {
		_TypeHint = (FieldTypeHint)typeHint;
		_Data = data;
	}

	public FieldVal(FieldTypeHint typeHint, byte[] data) {
		_TypeHint = typeHint;
		_Data = data;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public uint CountEncodeLength() {
		Debug.Assert(typeof(FieldTypeHintInt) == typeof(uint));
		// Given that we're returning a `uint`, the following computation is
		// guaranteed to not overflow, since `Array.Length` is limited to
		// `int.MaxValue` (which is ~2GiB).
		return (uint)VarInts.Length((uint)_TypeHint) + (uint)_Data.Length;
	}
}
