namespace Kokoro;

public sealed partial class FieldVal {

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static long ReadInt64(byte[] data) {
		ref byte r0 = ref data.DangerousGetReference();
		int n = data.Length;

		const int S = sizeof(long);
		long mask = (1L << (n << 3)) - 1;
		mask |= (long)((S - n) >> 31);

		long r;
		if (S > U.SizeOf<nint>()) {
			r = default;
			U.CopyBlock(
				destination: ref U.As<long, byte>(ref r),
				source: ref r0,
				byteCount: (uint)Math.Min(data.Length, S)
			);
		} else {
			r = U.As<byte, long>(ref r0);
		}

		r = r.LittleEndian() & mask;
		r = -((~mask >> 1) & r) | r;
		return r;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static int ReadInt32(byte[] data) {
		ref byte r0 = ref data.DangerousGetReference();
		int n = data.Length;

		const int S = sizeof(int);
		int mask = (1 << (n << 3)) - 1;
		mask |= (S - n) >> 31;

		int r;
		if (S > U.SizeOf<nint>()) {
			r = default;
			U.CopyBlock(
				destination: ref U.As<int, byte>(ref r),
				source: ref r0,
				byteCount: (uint)Math.Min(data.Length, S)
			);
		} else {
			r = U.As<byte, int>(ref r0);
		}

		r = r.LittleEndian() & mask;
		r = -((~mask >> 1) & r) | r;
		return r;
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
