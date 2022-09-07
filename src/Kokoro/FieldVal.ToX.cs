namespace Kokoro;

public sealed partial class FieldVal {

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static long ReadInt64(FieldTypeHint type, byte[] data) {
		if (sizeof(long) <= U.SizeOf<nint>()) {
			return ReadInt64_NativeSupport(type, data);
		} else {
			return ReadInt64_Fallback(type, data);
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static long ReadInt64_NativeSupport(FieldTypeHint type, byte[] data) {
		int m1WhenSigned = type.WhenIntOrUIntRetM1IfInt();

		ref byte r0 = ref data.DangerousGetReference();
		int n = data.Length;

		const int S = sizeof(long);
		n -= S; n = ((n >> 31) & n) + S; // `min(n,S)` without branching

		int shift = (n << 3) - 1;
		long mask = (2L << shift) - 1;
		long r = U.As<byte, long>(ref r0).LittleEndian() & mask;
		return (-((~mask >> 1) & r) & m1WhenSigned) | r;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static long ReadInt64_Fallback(FieldTypeHint type, byte[] data) {
		int m1WhenSigned = type.WhenIntOrUIntRetM1IfInt();

		ref byte r0 = ref data.DangerousGetReference();
		int n = data.Length;

		const int S = sizeof(long);
		n -= S; n = ((n >> 31) & n) + S; // `min(n,S)` without branching

		long mask = (long)m1WhenSigned << ((n << 3) - 1);
		long v = default;
		U.CopyBlock(
			destination: ref U.As<long, byte>(ref v),
			source: ref r0,
			byteCount: (uint)n
		);
		long r = v.LittleEndian();
		return -(mask & r) | r;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static int ReadInt32(FieldTypeHint type, byte[] data) {
		if (sizeof(int) <= U.SizeOf<nint>()) {
			return ReadInt32_NativeSupport(type, data);
		} else {
			return ReadInt32_Fallback(type, data);
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static int ReadInt32_NativeSupport(FieldTypeHint type, byte[] data) {
		int m1WhenSigned = type.WhenIntOrUIntRetM1IfInt();

		ref byte r0 = ref data.DangerousGetReference();
		int n = data.Length;

		const int S = sizeof(int);
		n -= S; n = ((n >> 31) & n) + S; // `min(n,S)` without branching

		int shift = (n << 3) - 1;
		int mask = (2 << shift) - 1;
		int r = U.As<byte, int>(ref r0).LittleEndian() & mask;
		return (-((~mask >> 1) & r) & m1WhenSigned) | r;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static int ReadInt32_Fallback(FieldTypeHint type, byte[] data) {
		int m1WhenSigned = type.WhenIntOrUIntRetM1IfInt();

		ref byte r0 = ref data.DangerousGetReference();
		int n = data.Length;

		const int S = sizeof(int);
		n -= S; n = ((n >> 31) & n) + S; // `min(n,S)` without branching

		int mask = m1WhenSigned << ((n << 3) - 1);
		int v = default;
		U.CopyBlock(
			destination: ref U.As<int, byte>(ref v),
			source: ref r0,
			byteCount: (uint)n
		);
		int r = v.LittleEndian();
		return -(mask & r) | r;
	}

	// --

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public string? GetText() {
		if (_TypeHint == FieldTypeHint.Text) {
			return TextUtils.UTF8ToString(_Data);
		}
		return null;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public ReadOnlySpan<byte> GetBlob() {
		if (_TypeHint == FieldTypeHint.Blob) {
			return _Data;
		}
		return default;
	}
}
