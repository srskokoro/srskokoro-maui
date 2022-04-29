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

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public override bool Equals(object? obj) {
		// Ternary operator returning true/false prevents redundant asm generation:
		// See, https://github.com/dotnet/runtime/issues/4207#issuecomment-147184273
		return Equals(obj as StringKey) ? true : false;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public bool Equals(StringKey? other) {
		if ((object)this != other) {
			if (other is null || Value != other.Value) {
				return false;
			}
		}
		return true;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static bool operator ==(StringKey? left, StringKey? right) {
		if ((object?)left != right) {
			if (left is null || right is null || left.Value != right.Value) {
				return false;
			}
		}
		return true;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool operator !=(StringKey? left, StringKey? right) {
		if ((object?)left != right) {
			if (left is null || right is null || left.Value != right.Value) {
				return true;
			}
		}
		return false;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public override int GetHashCode() => _HashCode;

	public object Clone() => this;
}
