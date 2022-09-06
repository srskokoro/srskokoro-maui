namespace Kokoro;

public sealed partial class FieldVal {

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static long ReadInt64(FieldTypeHint type, byte[] data) {
		int m1WhenSigned = type.WhenIntOrUIntRetM1IfInt();

		ref byte r0 = ref data.DangerousGetReference();
		int n = data.Length;

		const int S = sizeof(long);
		n -= S; n = ((n >> 31) & n) + S; // `min(n,S)` without branching
		long mask = (1L << (n << 3)) - 1;

		long r;
		if (S > U.SizeOf<nint>()) {
			r = default;
			U.CopyBlock(
				destination: ref U.As<long, byte>(ref r),
				source: ref r0,
				byteCount: (uint)n
			);
		} else {
			r = U.As<byte, long>(ref r0);
		}

		r = r.LittleEndian() & mask;
		return (-((~mask >> 1) & r) & m1WhenSigned) | r;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static int ReadInt32(FieldTypeHint type, byte[] data) {
		int m1WhenSigned = type.WhenIntOrUIntRetM1IfInt();

		ref byte r0 = ref data.DangerousGetReference();
		int n = data.Length;

		const int S = sizeof(int);
		n -= S; n = ((n >> 31) & n) + S; // `min(n,S)` without branching
		int mask = (1 << (n << 3)) - 1;

		int r;
		if (S > U.SizeOf<nint>()) {
			r = default;
			U.CopyBlock(
				destination: ref U.As<int, byte>(ref r),
				source: ref r0,
				byteCount: (uint)n
			);
		} else {
			r = U.As<byte, int>(ref r0);
		}

		r = r.LittleEndian() & mask;
		return (-((~mask >> 1) & r) & m1WhenSigned) | r;
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
