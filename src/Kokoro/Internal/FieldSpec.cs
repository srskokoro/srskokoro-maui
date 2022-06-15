namespace Kokoro.Internal;
using CommunityToolkit.HighPerformance.Helpers;

internal readonly struct FieldSpec {
	// Expected bit layout:
	// - The 2 LSBs represent the field storage type.
	// - The 3rd LSB (at bit index 2) is set if the field is an alias.
	// - The remaining bits serve as the index of the field in a field list.
	//
	// This corresponds to the `idx_a_sto` column of the `SchemaToField` table
	// in the collection's SQLite DB.
	public readonly uint Value;

	public int Index {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => (int)(Value >> 3);
	}

	public bool IsAlias {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => BitHelper.HasFlag(Value, 2);
	}

	public FieldStorageType StoType {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => (FieldStorageType)(Value & 0xb11);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public FieldSpec(int value) => Value = (uint)value;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public FieldSpec(uint value) => Value = value;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static implicit operator int(FieldSpec fspec) => (int)fspec.Value;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static implicit operator uint(FieldSpec fspec) => fspec.Value;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static implicit operator FieldSpec(int value) => new(value);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static implicit operator FieldSpec(uint value) => new(value);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public FieldSpec(int index, FieldStorageType sto) {
		Debug.Assert(((FieldStorageTypeInt)sto & 0b11) == (FieldStorageTypeInt)sto);
		Value = (uint)index << 3 | (uint)sto;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public FieldSpec(int index, bool isAlias, FieldStorageType sto) {
		Debug.Assert(((FieldStorageTypeInt)sto & 0b11) == (FieldStorageTypeInt)sto);
		Value = (uint)index << 3 | (uint)isAlias.ToByte() << 2 | (uint)sto;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public override string ToString() => Value.ToString();
}
