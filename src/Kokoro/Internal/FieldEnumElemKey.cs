namespace Kokoro.Internal;

internal readonly struct FieldEnumElemKey {
	/// Expected bit layout:
	/// - The 6 LSBs indicate the enumeration group number of the field enum.
	/// - The remaining bits serve as the field enum element's index under the
	/// field enum.
	///
	/// This corresponds to the `idx_e` column of the `<see cref="Prot.SchemaToEnumElem"/>`
	/// table in the collection's SQLite DB.
	public readonly uint Value;

	private const int EnumGroup_Mask = 0b_11_1111;
	private const int Index_Shift = 6;

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

	public const int MaxEnumGroup = EnumGroup_Mask;

	public int EnumGroup {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => (int)(Value & EnumGroup_Mask);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private FieldEnumElemKey(int value) => Value = (uint)value;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private FieldEnumElemKey(uint value) => Value = value;

	// BONUS: We can compare `FieldEnumElemKey` against an `int` (due to the
	// implicit cast) without needing to define some comparison operators.
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static implicit operator int(FieldEnumElemKey fspec) => (int)fspec.Value;

	// BONUS: We can compare `FieldEnumElemKey` against a `uint` (due to the
	// implicit cast) without needing to define some comparison operators.
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static implicit operator uint(FieldEnumElemKey fspec) => fspec.Value;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static implicit operator FieldEnumElemKey(int value) => new(value);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static implicit operator FieldEnumElemKey(uint value) => new(value);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public FieldEnumElemKey(int index, int enumGroup) {
		DAssert_Valid(index, enumGroup);
		Value = (uint)index << Index_Shift | (uint)enumGroup;
	}

	[Conditional("DEBUG")]
	public void DAssert_Valid() => DAssert_Valid(Index, EnumGroup);

	[Conditional("DEBUG")]
	private static void DAssert_Valid(int index, int enumGroup) {
		Debug.Assert((uint)index <= (uint)MaxIndex, $"{nameof(Index)}: {index}");
		Debug.Assert((uint)enumGroup <= (uint)MaxEnumGroup, $"{nameof(EnumGroup)}: {enumGroup}");
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public override string ToString() => Value.ToString();
}
