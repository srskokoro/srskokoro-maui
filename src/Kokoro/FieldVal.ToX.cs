namespace Kokoro;
using Kokoro.Internal;
using System.Buffers.Binary;
using System.Numerics;

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
		Debug.Assert(type.IsIntOrUInt());
		int m1WhenSigned = type.WhenIntOrUIntRetM1IfInt();

		ref byte b0 = ref data.DangerousGetReference();
		int n = data.Length;

		const int MaxDataLength = FieldedEntity.MaxFieldValsLength;
		Debug.Assert((uint)MaxDataLength << 3 >> 3 == MaxDataLength);
		// ^- The above asserts that, the shift below is tolerable to be
		// undefined, should the data length exceed the expected maximum.
		int shift = n << 3;
		long mask = ((long)((uint)(shift - 32) >> 31) << shift) - 1;

		long r = U.As<byte, long>(ref b0).LittleEndian() & mask;
		return (-((~mask >> 1) & r) & m1WhenSigned) | r;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static int ReadInt32_NativeSupport(FieldTypeHint type, byte[] data) {
		Debug.Assert(type.IsIntOrUInt());
		int m1WhenSigned = type.WhenIntOrUIntRetM1IfInt();

		ref byte b0 = ref data.DangerousGetReference();
		int n = data.Length;

		const int MaxDataLength = FieldedEntity.MaxFieldValsLength;
		Debug.Assert((uint)MaxDataLength << 3 >> 3 == MaxDataLength);
		// ^- The above asserts that, the shift below is tolerable to be
		// undefined, should the data length exceed the expected maximum.
		int shift = n << 3;
		int mask = ((int)((uint)(shift - 32) >> 31) << shift) - 1;

		int r = U.As<byte, int>(ref b0).LittleEndian() & mask;
		return (-((~mask >> 1) & r) & m1WhenSigned) | r;
	}


	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static long ReadInt64_Fallback(FieldTypeHint type, byte[] data) {
		Debug.Assert(type.IsIntOrUInt());
		int m1WhenSigned = type.WhenIntOrUIntRetM1IfInt();

		ref byte b0 = ref data.DangerousGetReference();
		int n = data.Length;

		const int S = sizeof(long);
		n -= S; n = ((n >> 31) & n) + S; // `min(n,S)` without branching

		long mask = (long)m1WhenSigned << ((n << 3) - 1);
		long v = default;
		U.CopyBlock(
			destination: ref U.As<long, byte>(ref v),
			source: ref b0,
			byteCount: (uint)n
		);
		long r = v.LittleEndian();
		return -(mask & r) | r;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static int ReadInt32_Fallback(FieldTypeHint type, byte[] data) {
		Debug.Assert(type.IsIntOrUInt());
		int m1WhenSigned = type.WhenIntOrUIntRetM1IfInt();

		ref byte b0 = ref data.DangerousGetReference();
		int n = data.Length;

		const int S = sizeof(int);
		n -= S; n = ((n >> 31) & n) + S; // `min(n,S)` without branching

		int mask = m1WhenSigned << ((n << 3) - 1);
		int v = default;
		U.CopyBlock(
			destination: ref U.As<int, byte>(ref v),
			source: ref b0,
			byteCount: (uint)n
		);
		int r = v.LittleEndian();
		return -(mask & r) | r;
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
		if (type.IsIntOrUInt()) return ReadInt32(type, _Data);
		if (type.IsZeroOrOne()) return type.GetZeroOrOne();
		if (type == FieldTypeHint.Real) {
			return FallbackReadInt32_FromReal(_Data);
		}
		return 0;
	}

	public long GetInt64() {
		var type = _TypeHint;
		if (type.IsIntOrUInt()) return ReadInt64(type, _Data);
		if (type.IsZeroOrOne()) return type.GetZeroOrOne();
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

		if (type.IsIntOrUInt()) {
			data = _Data;
			n = data.Length;
			if (n <= sizeof(long)) {
				long r = ReadInt64(type, data);
				if (type == FieldTypeHint.Int) return r;
				return (ulong)r;
			} else {
				goto FromBigInt;
			}
		} else if (type.IsZeroOrOne()) {
			return type.GetZeroOrOne();
		} else if (type == FieldTypeHint.Real) {
			return ReadReal(_Data);
		}
		return 0;

	FromBigInt:
		return (double)new BigInteger(data,
			isUnsigned: type == FieldTypeHint.UInt,
			isBigEndian: false);
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
