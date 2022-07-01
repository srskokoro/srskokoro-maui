namespace Kokoro;

public sealed class StringKey : IComparable, IComparable<StringKey>, IEquatable<StringKey>, ICloneable {

	public readonly string Value;

	private readonly int _HashCode;

	internal static int ApproxSizeOverhead {
		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		get =>
			/// For <see cref="StringKey"/>: sync block + type handle + <see cref="Value"/> + <see cref="_HashCode"/> (with padding)
			/// For <see cref="string"/>: sync block + type handle + (<see cref="string.Length"/> + null-terminator + padding)
			IntPtr.Size * 6 + sizeof(int) * 2
			;
		// See also,
		// - https://stackoverflow.com/a/14287092
		// - https://stackoverflow.com/a/5691114
	}

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
	public int CompareTo(StringKey? other) {
		if (other is not null) {
			return string.CompareOrdinal(Value, other.Value);
		}
		return 1;
	}

	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	public override bool Equals([NotNullWhen(true)] object? obj) {
		// The following arrangement allows `string.Equals()` to be inlined,
		// while also producing a decent asm output.

		if ((object)this != obj) {
			// This becomes a conditional jump forward to not favor it
			goto CmpStr;
		}
	EQ:
		return true;

	CmpStr:
		// NOTE: The longest execution is when the sequence is equal. So we
		// favor that instead of the early outs leading to the not-equals case.

		// NOTE: This explicit null-check seems to produce better asm (for now)
		// than without it.
		if (obj is null) goto NE; // A conditional jump forward to not favor it
		if (obj is not StringKey other) goto NE; // ^

		// NOTE: Strangely, `string.Equals()` doesn't get inlined if we simply
		// returned its boolean result or a corresponding boolean result.
		if (string.Equals(Value, other.Value)) {
			goto EQ; // This becomes a conditional jump backward to favor it
		}
	NE:
		return false;
	}

	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	public bool Equals([NotNullWhen(true)] StringKey? other) {
		// The following arrangement allows `string.Equals()` to be inlined,
		// while also producing a decent asm output.

		if ((object)this != other) {
			// This becomes a conditional jump forward to not favor it
			goto CmpStr;
		}
	EQ:
		return true;

	CmpStr:
		// NOTE: The longest execution is when the sequence is equal. So we
		// favor that instead of the early outs leading to the not-equals case.

		if (other is null) goto NE; // A conditional jump forward to not favor it

		// NOTE: Strangely, `string.Equals()` doesn't get inlined if we simply
		// returned its boolean result or a corresponding boolean result.
		if (string.Equals(Value, other.Value)) {
			goto EQ; // This becomes a conditional jump backward to favor it
		}
	NE:
		return false;
	}

	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	public static bool operator ==(StringKey? a, StringKey? b) {
		// The following arrangement allows `string.Equals()` to be inlined,
		// while also producing a decent asm output.

		if ((object?)a != b) {
			// This becomes a conditional jump forward to not favor it
			goto CmpStr;
		}
	EQ:
		return true;

	CmpStr:
		// NOTE: The longest execution is when the sequence is equal. So we
		// favor that instead of the early outs leading to the not-equals case.

		if (a is null) goto NE; // A conditional jump forward to not favor it
		if (b is null) goto NE; // ^

		// NOTE: Strangely, `string.Equals()` doesn't get inlined if we simply
		// returned its boolean result or a corresponding boolean result.
		if (string.Equals(a.Value, b.Value)) {
			goto EQ; // This becomes a conditional jump backward to favor it
		}
	NE:
		return false;
	}

	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	public static bool operator !=(StringKey? a, StringKey? b) {
		// The following arrangement allows `string.Equals()` to be inlined,
		// while also producing a decent asm output.

		if ((object?)a != b) {
			// This becomes a conditional jump forward to not favor it
			goto CmpStr;
		}
	EQ:
		return false;

	CmpStr:
		// NOTE: The longest execution is when the sequence is equal. So we
		// favor that instead of the early outs leading to the not-equals case.

		if (a is null) goto NE; // A conditional jump forward to not favor it
		if (b is null) goto NE; // ^

		// NOTE: Strangely, `string.Equals()` doesn't get inlined if we simply
		// returned its boolean result or a corresponding boolean result.
		if (string.Equals(a.Value, b.Value)) {
			goto EQ; // This becomes a conditional jump backward to favor it
		}
	NE:
		return true;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public override int GetHashCode() => _HashCode;

	public object Clone() => this;
}
