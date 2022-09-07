namespace Kokoro;
using Kokoro.Internal;
using static System.Buffers.Binary.BinaryPrimitives;

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
	internal static int ReadInt32(FieldTypeHint type, byte[] data) {
		if (sizeof(int) <= U.SizeOf<nint>()) {
			return ReadInt32_NativeSupport(type, data);
		} else {
			return ReadInt32_Fallback(type, data);
		}
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

	// --

	[MethodImpl(MethodImplOptions.NoInlining)]
	internal static double ReadReal64_NoInline(byte[] data) {
		ref byte b0 = ref data.DangerousGetReference();

		double r;
		goto Switch;

	Return: // Ensures epilogue happens here instead
		return r;

	Switch:
		switch (data.Length) {
			default: {
				r = !BitConverter.IsLittleEndian
					? BitConverter.Int64BitsToDouble(ReverseEndianness(U.As<byte, long>(ref b0)))
					: U.As<byte, double>(ref b0);
				goto Return;
			}
			case 7:
			case 6:
			case 5:
			case 4: {
				r = !BitConverter.IsLittleEndian
					? BitConverter.Int32BitsToSingle(ReverseEndianness(U.As<byte, int>(ref b0)))
					: U.As<byte, float>(ref b0);
				goto Return;
			}
			case 3:
			case 2: {
				r = (double)(!BitConverter.IsLittleEndian
					? BitConverter.Int16BitsToHalf(ReverseEndianness(U.As<byte, short>(ref b0)))
					: U.As<byte, Half>(ref b0));
				goto Return;
			}
			case 1:
			case 0: {
				r = 0;
				goto Return;
			}
		}
	}

	// --

	[MethodImpl(MethodImplOptions.NoInlining)]
	private int FallbackReadInt32_FromReal64() => (int)ReadReal64_NoInline(_Data);

	[MethodImpl(MethodImplOptions.NoInlining)]
	private long FallbackReadInt64_FromReal64() => (long)ReadReal64_NoInline(_Data);

	[MethodImpl(MethodImplOptions.NoInlining)]
	private double FallbackReadReal64_FromInt64(FieldTypeHint type) {
		long r = ReadInt64(type, _Data);
		if (type == FieldTypeHint.Int) return r;
		Debug.Assert(type == FieldTypeHint.UInt);
		return (ulong)r;
	}

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
			return FallbackReadInt32_FromReal64();
		}
		return default;
	}

	public long GetInt64() {
		var type = _TypeHint;
		if (type.IsIntOrUInt()) return ReadInt64(type, _Data);
		if (type.IsZeroOrOne()) return type.GetZeroOrOne();
		if (type == FieldTypeHint.Real) {
			return FallbackReadInt64_FromReal64();
		}
		return default;
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

	public Half GetReal16() {
		var type = _TypeHint;
		if (type == FieldTypeHint.Real) return (Half)ReadReal64_NoInline(_Data);
		if (type.IsZeroOrOne()) return (Half)type.GetZeroOrOne();
		if (type.IsIntOrUInt()) {
			return (Half)FallbackReadReal64_FromInt64(type);
		}
		return default;
	}

	public float GetReal32() {
		var type = _TypeHint;
		if (type == FieldTypeHint.Real) return (float)ReadReal64_NoInline(_Data);
		if (type.IsZeroOrOne()) return type.GetZeroOrOne();
		if (type.IsIntOrUInt()) {
			return (float)FallbackReadReal64_FromInt64(type);
		}
		return default;
	}

	public double GetReal64() {
		var type = _TypeHint;
		if (type == FieldTypeHint.Real) return ReadReal64_NoInline(_Data);
		if (type.IsZeroOrOne()) return type.GetZeroOrOne();
		if (type.IsIntOrUInt()) {
			return FallbackReadReal64_FromInt64(type);
		}
		return default;
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
