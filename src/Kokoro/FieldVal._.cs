namespace Kokoro;
using Blake2Fast.Implementation;
using Kokoro.Common.Util;

public sealed partial class FieldVal {

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
}
