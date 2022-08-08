namespace Kokoro.Internal;

internal readonly struct FieldSpec {
	/// Expected bit layout:
	/// - The 2 LSBs represent the field store type.
	/// - The remaining bits serve as the index of the field in a field list.
	///
	/// This corresponds to the `idx_sto` column of the `<see cref="Prot.SchemaToField"/>`
	/// table in the collection's SQLite DB.
	public readonly uint Value;

	private const int StoreType_Mask = 0b11;
	private const int Index_Shift = 2;

	// --

	public const int MaxIndex = int.MaxValue >> Index_Shift;

	public const uint IndexIncrement = 1 << Index_Shift;

	public int Index {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => (int)(Value >> Index_Shift);
	}

	public FieldStoreType StoreType {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => (FieldStoreType)(Value & StoreType_Mask);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public FieldSpec(int value) => Value = (uint)value;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public FieldSpec(uint value) => Value = value;

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
		Debug.Assert(index <= MaxIndex);
		Debug.Assert(((FieldStoreTypeInt)sto & StoreType_Mask) == (FieldStoreTypeInt)sto);
		Value = (uint)index << Index_Shift | (uint)sto;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public override string ToString() => Value.ToString();
}
