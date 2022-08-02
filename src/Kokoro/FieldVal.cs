namespace Kokoro;
using Blake2Fast.Implementation;
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
		FieldTypeHintUInt typeHint = (FieldTypeHintUInt)_TypeHint;
		if (typeHint != (FieldTypeHintUInt)FieldTypeHint.Null) {
			Debug.Assert(typeof(FieldTypeHintUInt) == typeof(uint));
			Span<byte> typeHintBuffer = stackalloc byte[VarInts.MaxLength32];
			int typeHintLength = VarInts.Write(typeHintBuffer, typeHint);

			byte[] data = _Data;
			uint encodeLength = (uint)typeHintLength + (uint)data.Length;
			/// See also, <see cref="CountEncodeLength"/>

			hasher.UpdateLE(encodeLength); // i.e., length-prepended

			typeHintBuffer = typeHintBuffer.Slice(0, typeHintLength);
			hasher.Update(typeHintBuffer);

			hasher.Update(data);
		} else {
			hasher.UpdateLE((uint)0); // i.e., zero length
		}
	}
}
