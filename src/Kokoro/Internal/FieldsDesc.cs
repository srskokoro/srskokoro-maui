namespace Kokoro.Internal;
using CommunityToolkit.HighPerformance.Helpers;
using Kokoro.Common.Util;

internal readonly struct FieldsDesc {
	/// Expected bit layout:
	/// - The 2 LSBs represent the number of bytes used to store a field offset
	/// integer, with `0b00` (or `0x0`) meaning 1 byte, `0b11` (or `0x3`) meaning
	/// 4 bytes, etc. -- a field offset is expected to be a 32-bit integer.
	/// - The 3rd LSB (at bit index 2) is set if the descriptor is for the hot
	/// store and there's cold data kept in a separate cold store. That is, the
	/// field store is a hot store with a non-empty cold store counterpart.
	/// - The remaining bits indicate the number of fields (and field offset
	/// integers) in the fielded data.
	///
	/// This corresponds to the data descriptor stored in columns like `Item.data`,
	/// `ItemToColdStore.data` and `Schema.data` in the collection's SQLite DB.
	public readonly uint Value;

	private const int FOffsetSizeM1Or0_Mask = 0b11; // 3
	private const int HasColdComplement_Shift = 2;
	private const int FieldCount_Shift = 3;

	// --

	public const int MaxFOffsetSizeM1Or0 = FOffsetSizeM1Or0_Mask; // 3
	public const int MaxFOffsetSize = FOffsetSizeM1Or0_Mask + 1; // 3 + 1 == 4

	public const int MaxFieldCount = (int.MaxValue >> 1) / MaxFOffsetSize; // NOTE: `(n >> 1)/4 == n >> 3`

	// NOTE: Resulting constant same as `int.MaxValue`
	public const int MaxValue =
		MaxFieldCount << FieldCount_Shift
			| (1 << HasColdComplement_Shift)
			| MaxFOffsetSizeM1Or0;

	public const byte ByteArea_HasColdComplement_Bit = 1 << HasColdComplement_Shift;

	// --

	public const int VarIntLengthForEmpty = VarInts.LengthForZero;

	public static FieldsDesc Empty {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => default;
	}

	public int FieldCount {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => (int)(Value >> FieldCount_Shift);
	}

	public bool HasColdComplement {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => BitHelper.HasFlag(Value, HasColdComplement_Shift);
	}

	public byte ByteArea_HasColdComplement {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => (byte)Value;
	}

	public int FOffsetSize {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => FOffsetSizeM1Or0 + 1;
	}

	public int FOffsetSizeM1Or0 {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => (int)Value & FOffsetSizeM1Or0_Mask;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public FieldsDesc(int value) => Value = (uint)value;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public FieldsDesc(uint value) => Value = value;

	// BONUS: We can compare `FieldsDesc` against an `int` (due to the implicit
	// cast) without needing to define some comparison operators.
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static implicit operator int(FieldsDesc fdesc) => (int)fdesc.Value;

	// BONUS: We can compare `FieldsDesc` against a `uint` (due to the implicit
	// cast) without needing to define some comparison operators.
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static implicit operator uint(FieldsDesc fdesc) => fdesc.Value;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static implicit operator FieldsDesc(int value) => new(value);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static implicit operator FieldsDesc(uint value) => new(value);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public FieldsDesc(int fCount, int fOffsetSizeM1Or0) {
		Value = (uint)fCount << FieldCount_Shift | (uint)fOffsetSizeM1Or0;

		Debug.Assert(FieldCount == fCount);
		Debug.Assert(FOffsetSizeM1Or0 == fOffsetSizeM1Or0);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public FieldsDesc(int fCount, bool fHasCold, int fOffsetSizeM1Or0) {
		Value = (uint)fCount << FieldCount_Shift
			| (uint)fHasCold.ToByte() << HasColdComplement_Shift
			| (uint)fOffsetSizeM1Or0;

		Debug.Assert(FieldCount == fCount);
		Debug.Assert(HasColdComplement == fHasCold);
		Debug.Assert(FOffsetSizeM1Or0 == fOffsetSizeM1Or0);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public override string ToString() => Value.ToString();
}
