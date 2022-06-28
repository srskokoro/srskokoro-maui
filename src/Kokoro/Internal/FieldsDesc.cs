namespace Kokoro.Internal;
using CommunityToolkit.HighPerformance.Helpers;

internal readonly struct FieldsDesc {
	// Expected bit layout:
	// - The 2 LSBs represent the number of bytes used to store a field offset
	// integer, with `0b00` (or `0x0`) meaning 1 byte, `0b11` (or `0x3`) meaning
	// 4 bytes, etc. -- a field offset is expected to be 32-bit integer.
	// - The 3rd LSB (at bit index 2) is set if there's a separate cold data.
	// - The remaining bits indicate the number of fields (and field offset
	// integers) in the fielded data.
	//
	// This corresponds to the data descriptor stored in columns like `Item.data`,
	// `ItemToColdField.data` and `Schema.data` in the collection's SQLite DB.
	public readonly uint Value;

	// --

	public const int MaxFOffsetSizeM1Or0 = 0b11; // 3
	public const int MaxFOffsetSize = 0b11 + 1; // 3 + 1 == 4

	/// 3 bits == 2 bits for <see cref="FOffsetSizeM1Or0"/> + 1 bit for <see cref="HasColdData"/>
	public const int MaxFieldCount = int.MaxValue >> 3;

	public const int MaxValue = int.MaxValue;

	// --

	public int FieldCount {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => (int)(Value >> 3);
	}

	public bool HasColdData {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => BitHelper.HasFlag(Value, 2);
	}

	public int FOffsetSize {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => FOffsetSizeM1Or0 + 1;
	}

	public int FOffsetSizeM1Or0 {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => (int)Value & 0b11;
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
		Value = (uint)fCount << 3 | (uint)fOffsetSizeM1Or0;

		Debug.Assert(FieldCount == fCount);
		Debug.Assert(FOffsetSizeM1Or0 == fOffsetSizeM1Or0);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public FieldsDesc(int fCount, bool fHasCold, int fOffsetSizeM1Or0) {
		Value = (uint)fCount << 3 | (uint)fHasCold.ToByte() << 2 | (uint)fOffsetSizeM1Or0;

		Debug.Assert(FieldCount == fCount);
		Debug.Assert(FOffsetSizeM1Or0 == fOffsetSizeM1Or0);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public override string ToString() => Value.ToString();
}
