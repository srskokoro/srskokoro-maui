namespace Kokoro.Internal;

internal readonly struct FieldSpec {
	/// Expected bit layout:
	/// - The 2 LSBs represent the field store type.
	/// - The next 6 LSBs indicate the enumeration group, with all zeroes
	/// indicating that the field doesn't use an enumeration for its values.
	/// - The remaining bits serve as the index of the field in a field list.
	///
	/// This corresponds to the `idx_e_sto` column of the `<see cref="Prot.SchemaToField"/>`
	/// table in the collection's SQLite DB.
	public readonly uint Value;

	private const int StoreType_Mask = 0b11;
	private const int EnumGroup_StoreType_Mask = 0b_1111_1111;
	private const int EnumGroup_Shift = 2;
	private const int Index_Shift = 8;

	public readonly int Int {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => this;
	}

	// --

	public const int MaxIndex = int.MaxValue >> Index_Shift;

	public const uint IndexIncrement = 1 << Index_Shift;

	public int Index {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => (int)(Value >> Index_Shift);
	}

	public const int MaxEnumGroup = EnumGroup_StoreType_Mask >> EnumGroup_Shift;

	public int EnumGroup {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => (int)((Value & EnumGroup_StoreType_Mask) >> EnumGroup_Shift);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public FieldSpec ClearEnumGroup() {
		const uint Mask = ~((uint)EnumGroup_StoreType_Mask >> EnumGroup_Shift << EnumGroup_Shift);
		return Value & Mask;
	}

	public FieldStoreType StoreType {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => (FieldStoreType)(Value & StoreType_Mask);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private FieldSpec(int value) => Value = (uint)value;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private FieldSpec(uint value) => Value = value;

	// BONUS: We can compare `FieldSpec` against an `int` (due to the implicit
	// cast) without needing to define some comparison operators.
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static implicit operator int(FieldSpec fspec) => (int)fspec.Value;

	// BONUS: We can compare `FieldSpec` against a `uint` (due to the implicit
	// cast) without needing to define some comparison operators.
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static implicit operator uint(FieldSpec fspec) => fspec.Value;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static implicit operator FieldSpec(int value) => new(value);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static implicit operator FieldSpec(uint value) => new(value);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public FieldSpec(int index, FieldStoreType sto) {
		DAssert_Valid(index, sto);
		Value = (uint)index << Index_Shift | (uint)sto;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public FieldSpec(int index, int enumGroup, FieldStoreType sto) {
		DAssert_Valid(index, enumGroup, sto);
		Value = (uint)index << Index_Shift | (uint)enumGroup << EnumGroup_Shift | (uint)sto;
	}

	[Conditional("DEBUG")]
	public void DAssert_Valid() => DAssert_Valid(Index, EnumGroup, StoreType);

	[Conditional("DEBUG")]
	private static void DAssert_Valid(int index, FieldStoreType sto) {
		Debug.Assert((uint)index <= (uint)MaxIndex, $"{nameof(Index)}: {index}");
		sto.DAssert_Defined();
	}

	[Conditional("DEBUG")]
	private static void DAssert_Valid(int index, int enumGroup, FieldStoreType sto) {
		Debug.Assert((uint)index <= (uint)MaxIndex, $"{nameof(Index)}: {index}");
		Debug.Assert((uint)enumGroup <= (uint)MaxEnumGroup, $"{nameof(EnumGroup)}: {enumGroup}");
		sto.DAssert_Defined();
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public override string ToString() => Value.ToString();
}
