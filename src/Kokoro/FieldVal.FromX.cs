namespace Kokoro;

public sealed partial class FieldVal {

	public static FieldVal Zero => ZeroOrOneInstHolder.Zero;
	public static FieldVal One => ZeroOrOneInstHolder.One;

	private static class IntDataCache {

		// NOTE: Shouldn't use static constructor for this. See,
		// - https://stackoverflow.com/a/71063929
		// - https://docs.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca1810
		//
		internal static readonly byte[][] Cache = Init();

		internal const int MinValue = -128; // Same as, `sbyte.MinValue`
		internal const int MaxValue = 255; // Same as, `byte.MaxValue` (not `sbyte.MaxValue`)

		internal const int Offset = -MinValue;
		internal const int Size = -MinValue + MaxValue + 1;

		private static byte[][] Init() {
			Debug.Assert(Size == 256 + 128);

			byte[][] r = new byte[Size][];
			ref byte[] r0 = ref r.DangerousGetReference();

			for (int v = MinValue, i = 0; i < 256; i++)
				U.Add(ref r0, i) = new byte[] { (byte)v++ };

			for (int i = 256; i < Size; i++)
				U.Add(ref r0, i) = U.Add(ref r0, (byte)i);

			return r;
		}
	}

	private static class IntInstCache {

		// NOTE: Shouldn't use static constructor for this. See,
		// - https://stackoverflow.com/a/71063929
		// - https://docs.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca1810
		//
		internal static readonly FieldVal[] Cache = Init();

		internal const int MinValue = -128; // Same as, `sbyte.MinValue`
		internal const int MaxValue = 127; // Same as, `sbyte.MaxValue`

		internal const int Offset = -MinValue;
		internal const int Size = -MinValue + MaxValue + 1;

		private static FieldVal[] Init() {
			Debug.Assert(MinValue <= MaxValue);

			ref byte[] d0 = ref IntDataCache.Cache.DangerousGetReference();

			FieldVal[] r = new FieldVal[Size];
			ref FieldVal r0 = ref r.DangerousGetReference();

			int i = 0;
			const int BeforeZero = Offset;
			const FieldTypeHint Type = FieldTypeHint.Int;

			Debug.Assert(Size <= IntDataCache.Size);
			Debug.Assert(Size > BeforeZero);
			Debug.Assert(Size >= 4);

			do {
				U.Add(ref r0, i) = new FieldVal(Type, U.Add(ref d0, i));
			} while (++i < BeforeZero);

			U.Add(ref r0, i++) = ZeroOrOneInstHolder.Zero;
			U.Add(ref r0, i++) = ZeroOrOneInstHolder.One;

			do {
				U.Add(ref r0, i) = new FieldVal(Type, U.Add(ref d0, i));
			} while (++i < Size);

			return r;
		}
	}

	private static class UIntInstCache {

		// NOTE: Shouldn't use static constructor for this. See,
		// - https://stackoverflow.com/a/71063929
		// - https://docs.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca1810
		//
		internal static readonly FieldVal[] Cache = Init();

		internal const uint MinValue = 0; // Same as, `byte.MinValue`
		internal const uint MaxValue = 255; // Same as, `byte.MaxValue`
		internal const int Size = (int)(MaxValue + 1);

		private static FieldVal[] Init() {
			Debug.Assert(MinValue <= MaxValue);

			const int DataCacheOffset = IntDataCache.Offset;
			const int DataCacheSubsetSize = IntDataCache.Size - DataCacheOffset;
			Debug.Assert(DataCacheSubsetSize >= 0);

			ref byte[] d0 = ref IntDataCache.Cache.DangerousGetReferenceAt(DataCacheOffset);

			FieldVal[] r = new FieldVal[Size];
			ref FieldVal r0 = ref r.DangerousGetReference();

			int i = 0;
			const FieldTypeHint Type = FieldTypeHint.UInt;

			Debug.Assert(Size <= DataCacheSubsetSize);
			Debug.Assert(Size >= 3);

			U.Add(ref r0, i++) = ZeroOrOneInstHolder.Zero;
			U.Add(ref r0, i++) = ZeroOrOneInstHolder.One;

			do {
				U.Add(ref r0, i) = new FieldVal(Type, U.Add(ref d0, i));
			} while (++i < Size);

			return r;
		}
	}

	private static class ZeroOrOneInstHolder {
		internal static readonly FieldVal Zero = new(FieldTypeHint.Zero);
		internal static readonly FieldVal One = new(FieldTypeHint.One);
		internal static readonly FieldVal[] ZeroOrOne = new FieldVal[2] { Zero, One };

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static FieldVal DangerousGetZeroOrOne(int aZeroOrOne) {
			Debug.Assert((aZeroOrOne & ~1) == 0);
			return ZeroOrOne.DangerousGetReferenceAt(aZeroOrOne);
		}
	}

	private FieldVal(FieldTypeHint typeHint) {
		_TypeHint = typeHint;
		_Data = Array.Empty<byte>();
	}


	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static byte[] MakeData<T>(T value, int count) where T : unmanaged {
		Debug.Assert(count <= U.SizeOf<T>());
		var data = new byte[count];
		U.CopyBlock(
			destination: ref data.DangerousGetReference(),
			source: ref U.As<T, byte>(ref value),
			byteCount: (uint)count
		);
		return data;
	}

	// --

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static FieldVal From(bool value)
		=> ZeroOrOneInstHolder.DangerousGetZeroOrOne((int)(value.ToByte() & 1));

	// --

	public static FieldVal From(sbyte value) {
		Debug.Assert(sbyte.MinValue >= IntInstCache.MinValue);
		Debug.Assert(sbyte.MaxValue <= IntInstCache.MaxValue);
		return IntInstCache.Cache.DangerousGetReferenceAt(value + IntInstCache.Offset);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static FieldVal From(short value) => From((int)value);

	public static FieldVal From(int value) {
		int i = value + IntInstCache.Offset;
		if ((uint)i < IntInstCache.Size) {
			return IntInstCache.Cache.DangerousGetReferenceAt((int)i);
		}
		return new(
			FieldTypeHint.Int,
			// NOTE: Avoids generation of unnecessary specialization of generic
			// method by reusing an existing one instead.
			MakeData<uint>(
				(uint)value.LittleEndian(),
				value.CountBytesNeededSigned()
			)
		);
	}

	public static FieldVal From(long value) {
		long i = value + IntInstCache.Offset;
		if ((ulong)i < IntInstCache.Size) {
			return IntInstCache.Cache.DangerousGetReferenceAt((int)i);
		}
		return new(
			FieldTypeHint.Int,
			// NOTE: Avoids generation of unnecessary specialization of generic
			// method by reusing an existing one instead.
			MakeData<ulong>(
				(ulong)value.LittleEndian(),
				value.CountBytesNeededSigned()
			)
		);
	}

	// --

	public static FieldVal From(byte value) {
		Debug.Assert(byte.MinValue >= UIntInstCache.MinValue);
		Debug.Assert(byte.MaxValue <= UIntInstCache.MaxValue);
		return UIntInstCache.Cache.DangerousGetReferenceAt(value);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static FieldVal From(ushort value) => From((uint)value);

	public static FieldVal From(uint value) {
		if (value < (uint)UIntInstCache.Size) {
			return UIntInstCache.Cache.DangerousGetReferenceAt((int)value);
		}
		return new(
			FieldTypeHint.UInt,
			MakeData<uint>(
				value.LittleEndian(),
				value.CountBytesNeeded()
			)
		);
	}

	public static FieldVal From(ulong value) {
		if (value < (ulong)UIntInstCache.Size) {
			return UIntInstCache.Cache.DangerousGetReferenceAt((int)value);
		}
		return new(
			FieldTypeHint.UInt,
			MakeData<ulong>(
				value.LittleEndian(),
				value.CountBytesNeeded()
			)
		);
	}

	// --

	public static FieldVal From(double value) {
		if (sizeof(long) <= U.SizeOf<nint>()) {
			long i = (long)value;
			if (i == value) return From(i);
		} else {
			int i = (int)value;
			if (i == value) return From(i);
		}
		return new(
			FieldTypeHint.Real,
			// NOTE: Avoids generation of unnecessary specialization of generic
			// method by reusing an existing one instead.
			MakeData<ulong>(
				BitConverter.DoubleToUInt64Bits(value).LittleEndian(),
				sizeof(ulong)
			)
		);
	}

	// --

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static FieldVal From(string text) => new(FieldTypeHint.Text, text.ToUTF8Bytes());

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static FieldVal From(byte[] blob) => new(FieldTypeHint.Blob, blob);
}
