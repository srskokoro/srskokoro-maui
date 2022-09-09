namespace Kokoro;
using Kokoro.Internal;
using System.Buffers.Binary;

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
	internal static int ReadInt32(FieldTypeHint type, byte[] data) {
		if (sizeof(int) <= U.SizeOf<nint>()) {
			return ReadInt32_NativeSupport(type, data);
		} else {
			return ReadInt32_Fallback(type, data);
		}
	}


	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static long ReadInt64_NativeSupport(FieldTypeHint type, byte[] data) {
		int n = data.Length;
		// When the data has no bytes, prevents `AccessViolationException` by
		// repointing the reference to somewhere else we can access.
		int m1WhenNoData = (n - 1) >> 31;
		ref long r0 = ref U.Add(ref U.As<byte, long>(ref data.DangerousGetReference()), m1WhenNoData);

		Debug.Assert(type.IsInt());
		// -1 : type is IntNZ and has data
		//  0 : type is IntNZ and has no data
		//  1 : type is IntP1
		int m1Or0Or1 = ((m1WhenNoData ^ ~1) & type.WhenIntRetM1IfIntNZ()) ^ 1;

		const int MaxDataLength = FieldedEntity.MaxFieldValsLength;
		Debug.Assert((uint)MaxDataLength << 3 >> 3 == MaxDataLength);
		// ^- The above asserts that, the shift below is tolerable to be
		// undefined, should the data length exceed the expected maximum.
		int shift = n << 3; // n * 8
		long mask = ((long)((uint)(shift - 32) >> 31) << shift) - 1;

		return ((r0.LittleEndian() ^ m1Or0Or1) & mask) ^ m1Or0Or1;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static int ReadInt32_NativeSupport(FieldTypeHint type, byte[] data) {
		int n = data.Length;
		// When the data has no bytes, prevents `AccessViolationException` by
		// repointing the reference to somewhere else we can access.
		int m1WhenNoData = (n - 1) >> 31;
		ref int r0 = ref U.Add(ref U.As<byte, int>(ref data.DangerousGetReference()), m1WhenNoData);

		Debug.Assert(type.IsInt());
		// -1 : type is IntNZ and has data
		//  0 : type is IntNZ and has no data
		//  1 : type is IntP1
		int m1Or0Or1 = ((m1WhenNoData ^ ~1) & type.WhenIntRetM1IfIntNZ()) ^ 1;

		const int MaxDataLength = FieldedEntity.MaxFieldValsLength;
		Debug.Assert((uint)MaxDataLength << 3 >> 3 == MaxDataLength);
		// ^- The above asserts that, the shift below is tolerable to be
		// undefined, should the data length exceed the expected maximum.
		int shift = n << 3; // n * 8
		int mask = ((int)((uint)(shift - 32) >> 31) << shift) - 1;

		return ((r0.LittleEndian() ^ m1Or0Or1) & mask) ^ m1Or0Or1;
	}


	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static long ReadInt64_Fallback(FieldTypeHint type, byte[] data) {
		ref byte b0 = ref data.DangerousGetReference();
		int n = data.Length;

		const int S = sizeof(long);
		// Min of `n` and `S` without branch -- See, https://graphics.stanford.edu/~seander/bithacks.html#IntegerMinOrMax
		n -= S; n = ((n >> 31) & n) + S; // Correct only if `n >= S + int.MinValue`

		Debug.Assert(type.IsInt());
		// -1 : type is IntNZ and has data
		//  0 : type is IntNZ and has no data
		//  1 : type is IntP1
		long v = (((-n >> 31) ^ 1) & type.WhenIntRetM1IfIntNZ()) ^ 1;

		long r = v.LittleEndian();
		U.CopyBlock(
			destination: ref U.As<long, byte>(ref r),
			source: ref b0,
			byteCount: (uint)n
		);

		return r.LittleEndian();
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static int ReadInt32_Fallback(FieldTypeHint type, byte[] data) {
		ref byte b0 = ref data.DangerousGetReference();
		int n = data.Length;

		const int S = sizeof(int);
		// Min of `n` and `S` without branch -- See, https://graphics.stanford.edu/~seander/bithacks.html#IntegerMinOrMax
		n -= S; n = ((n >> 31) & n) + S; // Correct only if `n >= S + int.MinValue`

		Debug.Assert(type.IsInt());
		// -1 : type is IntNZ and has data
		//  0 : type is IntNZ and has no data
		//  1 : type is IntP1
		int v = (((-n >> 31) ^ 1) & type.WhenIntRetM1IfIntNZ()) ^ 1;

		int r = v.LittleEndian();
		U.CopyBlock(
			destination: ref U.As<int, byte>(ref r),
			source: ref b0,
			byteCount: (uint)n
		);

		return r.LittleEndian();
	}


	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static double ReadReal(byte[] data) {
		if (sizeof(double) <= data.Length) {
			ref byte b0 = ref data.DangerousGetReference();
			return !BitConverter.IsLittleEndian
				? BitConverter.Int64BitsToDouble(BinaryPrimitives.ReverseEndianness(U.As<byte, long>(ref b0)))
				: U.As<byte, double>(ref b0);
		} else {
			return double.NaN;
		}
	}

	// --

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static int FallbackReadInt32_FromReal(byte[] data) => (int)ReadReal(data);

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static long FallbackReadInt64_FromReal(byte[] data) => (long)ReadReal(data);

	// --

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public sbyte GetInt8() => (sbyte)GetInt32();

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public short GetInt16() => (short)GetInt32();

	public int GetInt32() {
		var type = _TypeHint;
		if (type.IsInt()) return ReadInt32(type, _Data);
		if (type == FieldTypeHint.Real) {
			return FallbackReadInt32_FromReal(_Data);
		}
		return 0;
	}

	public long GetInt64() {
		var type = _TypeHint;
		if (type.IsInt()) return ReadInt64(type, _Data);
		if (type == FieldTypeHint.Real) {
			return FallbackReadInt64_FromReal(_Data);
		}
		return 0;
	}

	// --

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public byte GetUInt8() => (byte)GetInt8();

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public ushort GetUInt16() => (ushort)GetInt16();

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public uint GetUInt32() => (uint)GetInt32();

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public ulong GetUInt64() => (ulong)GetInt64();

	// --

	public double GetReal() {
		var type = _TypeHint;
		byte[] data;
		int n;

		if (type.IsInt()) {
			data = _Data;
			n = data.Length;
			if (n <= sizeof(long)) {
				long r = ReadInt64(type, data);
				if (r >= 0 || type == FieldTypeHint.IntNZ) {
					return (long)r;
				}
				return (ulong)r;
			} else {
				goto FromBigInt;
			}
		} else if (type == FieldTypeHint.Real) {
			return ReadReal(_Data);
		}
		return 0;

	FromBigInt:
		throw new NotImplementedException("TODO"); // TODO Implement
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
