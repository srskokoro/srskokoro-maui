namespace Kokoro;

public sealed class StringKey : IComparable, IComparable<StringKey>, IEquatable<StringKey>, ICloneable {

	public readonly string Value;

	private readonly int _HashCode;

	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	public StringKey(string value) {
		// NOTE: Hash computation is the same as that if we used `record` types instead.
		_HashCode = typeof(StringKey).GetHashCode() * -1521134295 + (Value = value).GetHashCode();
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public override string ToString() => Value;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public int CompareTo(object? obj) => CompareTo(obj as StringKey);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public int CompareTo(StringKey? other) => Value.CompareTo(other?.Value);

	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	public override bool Equals([NotNullWhen(true)] object? obj) {
		// The following arrangement allows `string.Equals()` to be inlined,
		// while also producing a decent asm output.

		if ((object)this != obj) {
			goto CmpStr; // This becomes a conditional jump
		}
	EQ:
		return true;
	NE:
		return false;

	CmpStr:
		// NOTE: The longest execution is when the sequence is equal. So we
		// favor that instead of the early outs leading to the not-equals case.

		if (obj is not StringKey other) goto NE; // A conditional jump to not favor it
		if (other is null) goto NE; // A conditional jump to not favor it

		if (string.Equals(Value, other.Value)) {
			goto EQ; // This becomes an unconditional jump
		} else
			goto NE; // This becomes a conditional jump to not favor it
	}

	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	public bool Equals([NotNullWhen(true)] StringKey? other) {
		// The following arrangement allows `string.Equals()` to be inlined,
		// while also producing a decent asm output.

		if ((object)this != other) {
			goto CmpStr; // This becomes a conditional jump
		}
	EQ:
		return true;
	NE:
		return false;

	CmpStr:
		// NOTE: The longest execution is when the sequence is equal. So we
		// favor that instead of the early outs leading to the not-equals case.

		if (other is null) goto NE; // A conditional jump to not favor it

		if (string.Equals(Value, other.Value)) {
			goto EQ; // This becomes an unconditional jump
		} else
			goto NE; // This becomes a conditional jump to not favor it
	}

	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	public static bool operator ==(StringKey? a, StringKey? b) {
		// The following arrangement allows `string.Equals()` to be inlined,
		// while also producing a decent asm output.

		if ((object?)a != b) {
			goto CmpStr; // This becomes a conditional jump
		}
	EQ:
		return true;
	NE:
		return false;

	CmpStr:
		// NOTE: The longest execution is when the sequence is equal. So we
		// favor that instead of the early outs leading to the not-equals case.

		if (a is null) goto NE; // A conditional jump to not favor it
		if (b is null) goto NE; // ^

		if (string.Equals(a.Value, b.Value)) {
			goto EQ; // This becomes an unconditional jump
		} else
			goto NE; // This becomes a conditional jump to not favor it
	}

	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	public static bool operator !=(StringKey? a, StringKey? b) {
		// The following arrangement allows `string.Equals()` to be inlined,
		// while also producing a decent asm output.

		if ((object?)a != b) {
			goto CmpStr; // This becomes a conditional jump
		}
	EQ:
		return false;
	NE:
		return true;

	CmpStr:
		// NOTE: The longest execution is when the sequence is equal. So we
		// favor that instead of the early outs leading to the not-equals case.

		if (a is null) goto NE; // A conditional jump to not favor it
		if (b is null) goto NE; // ^

		if (string.Equals(a.Value, b.Value)) {
			goto EQ; // This becomes an unconditional jump
		} else
			goto NE; // This becomes a conditional jump to not favor it
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public override int GetHashCode() => _HashCode;

	public object Clone() => this;
}
