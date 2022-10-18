namespace Kokoro;
using Blake2Fast.Implementation;
using Kokoro.Common.Util;

public sealed partial class FieldVal : IEquatable<FieldVal> {

	public static FieldVal Null => NullInstHolder.Instance;

	private static class NullInstHolder {
		internal static readonly FieldVal Instance = new();
	}

	private readonly FieldTypeHint _TypeHint;
	private readonly byte[] _Data;
	private int _HashCode;

	public FieldTypeHint TypeHint {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => _TypeHint;
	}

	public ReadOnlySpan<byte> Data {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => _Data.AsDangerousROSpan();
	}

	/// <seealso cref="DangerousResetHashCode()"/>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal byte[] DangerousGetDataBytes() => _Data;

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
		FieldTypeHintUInt typeHint = (FieldTypeHintUInt)_TypeHint;
		if (typeHint != (FieldTypeHintUInt)FieldTypeHint.Null) {
			// Given that we're returning a `uint`, the following computation is
			// guaranteed to not overflow, since `Array.Length` is limited to
			// `int.MaxValue` (which is ~2GiB).
			return (uint)VarInts.Length(typeHint) + (uint)_Data.Length;
		}
		return 0;
	}

	// --

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	[SkipLocalsInit]
	public void WriteTo(Stream destination) {
		FieldTypeHintUInt typeHint = (FieldTypeHintUInt)_TypeHint;
		if (typeHint != (FieldTypeHintUInt)FieldTypeHint.Null) {
			destination.WriteVarInt(typeHint);
			destination.Write(_Data);
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	[SkipLocalsInit]
	public void FeedTo(ref Blake2bHashState hasher) {
		FieldTypeHint typeHint = _TypeHint;

		Debug.Assert(typeof(FieldTypeHintUInt) == typeof(uint));
		hasher.UpdateLE((FieldTypeHintUInt)typeHint);

		if (typeHint != FieldTypeHint.Null) {
			ReadOnlySpan<byte> data = _Data.AsDangerousROSpan();
			hasher.UpdateLE(data.Length); // i.e., length-prepended
			hasher.Update(data);
		}
	}

	#region Equality and Comparability

	#region Equality

	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	public override bool Equals([NotNullWhen(true)] object? obj) {
		if ((object)this != obj) {
			if (obj is null) goto NE;
			if (obj is not FieldVal other) goto NE;
			if (_TypeHint != other._TypeHint) goto NE;
			if (!_Data.AsDangerousROSpan().SequenceEqual(
				other._Data.AsDangerousROSpan())) goto NE;
		}
		return true;
	NE:
		return false;
	}

	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	public bool Equals([NotNullWhen(true)] FieldVal? other) {
		if ((object)this != other) {
			if (other is null) goto NE;
			if (_TypeHint != other._TypeHint) goto NE;
			if (!_Data.AsDangerousROSpan().SequenceEqual(
				other._Data.AsDangerousROSpan())) goto NE;
		}
		return true;
	NE:
		return false;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[SkipLocalsInit]
	public override int GetHashCode() {
		int h = _HashCode;
		if (h != 0) return h;
		return ComputeHashCode();
	}

	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	[SkipLocalsInit]
	private int ComputeHashCode() {
		HashCode h = new();
		h.Add(_TypeHint);
		h.AddBytes(_Data.AsDangerousROSpan());
		return _HashCode = h.ToHashCode();
		// NOTE: AFAIK, the above is still thread-safe :P
		// - The worst that can happen is that each thread would have to
		// recompute the hash code.
	}

	/// <remarks>
	/// For when the underlying data (returned by <see cref="DangerousGetDataBytes()"/>)
	/// was modified.
	/// </remarks>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal void DangerousResetHashCode() => _HashCode = 0;

	#endregion

	#region Comparability

	// TODO Implement

	#endregion

	#region Relational Operators

	// TODO Implement

	#endregion

	#endregion
}
