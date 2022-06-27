namespace Kokoro.Internal;

internal readonly struct FieldsDesc {
	// Expected bit layout:
	// - The 2 LSBs represent the number of bytes used to store a field offset
	// integer, with `0b00` (or `0x0`) meaning 1 byte, `0b11` (or `0x3`) meaning
	// 4 bytes, etc. -- a field offset is expected to be 32-bit integer.
	// - The remaining bits indicate the number of fields (and field offset
	// integers) in the fielded data.
	//
	// This corresponds to the data descriptor stored in columns like `Item.data`,
	// `ItemToColdField.data` and `Schema.data` in the collection's SQLite DB.
	public readonly uint Value;

	// --

	public const int MaxFOSizeM1Or0 = 0b11; // 3
	public const int MaxFOSize = 0b11 + 1; // 3 + 1 == 4

	public const int MaxFieldCount = int.MaxValue / MaxFOSize; // Same as `>> 2`

	public const int MaxValue = MaxFieldCount << 2 | MaxFOSizeM1Or0; // Same as `int.MaxValue`

	// --

	public int FieldCount {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => (int)(Value >> 2);
	}

	public int FOSize {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => FOSizeM1Or0 + 1;
	}

	public int FOSizeM1Or0 {
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
	public FieldsDesc(int fCount, int foSizeM1Or0) {
		Value = (uint)fCount << 2 | (uint)foSizeM1Or0;

		Debug.Assert(FieldCount == fCount);
		Debug.Assert(FOSizeM1Or0 == foSizeM1Or0);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public override string ToString() => Value.ToString();
}
