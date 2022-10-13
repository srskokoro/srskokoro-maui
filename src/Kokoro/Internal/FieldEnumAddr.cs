namespace Kokoro.Internal;

internal readonly struct FieldEnumAddr {
	/// Expected bit layout:
	/// - The 6 LSBs indicate the enumeration group number of the field enum.
	/// - The remaining bits serve as the field enum element's index under the
	/// field enum.
	///
	/// This corresponds to the `idx_e` column of the `<see cref="Prot.SchemaToEnumElem"/>`
	/// table in the collection's SQLite DB.
	public readonly uint Value;

	private const int Group_Mask = 0b_11_1111;
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

	public const int MaxGroup = Group_Mask;

	public int Group {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => (int)(Value & Group_Mask);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private FieldEnumAddr(int value) => Value = (uint)value;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private FieldEnumAddr(uint value) => Value = value;

	// BONUS: We can compare `FieldEnumElemKey` against an `int` (due to the
	// implicit cast) without needing to define some comparison operators.
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static implicit operator int(FieldEnumAddr fspec) => (int)fspec.Value;

	// BONUS: We can compare `FieldEnumElemKey` against a `uint` (due to the
	// implicit cast) without needing to define some comparison operators.
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static implicit operator uint(FieldEnumAddr fspec) => fspec.Value;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static implicit operator FieldEnumAddr(int value) => new(value);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static implicit operator FieldEnumAddr(uint value) => new(value);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public FieldEnumAddr(int index, int group) {
		DAssert_Valid(index, group);
		Value = (uint)index << Index_Shift | (uint)group;
	}

	[Conditional("DEBUG")]
	public void DAssert_Valid() => DAssert_Valid(Index, Group);

	[Conditional("DEBUG")]
	private static void DAssert_Valid(int index, int group) {
		Debug.Assert((uint)index <= (uint)MaxIndex, $"{nameof(Index)}: {index}");
		Debug.Assert((uint)group <= (uint)MaxGroup, $"{nameof(Group)}: {group}");
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public override string ToString() => Value.ToString();
}
