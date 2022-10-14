namespace Kokoro;
using Kokoro.Common.Util;
using Kokoro.Internal;

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
		internal const int MaxValue = 255; // Same as, `byte.MaxValue` (not `sbyte.MaxValue`)

		internal const int Offset = -MinValue;
		internal const int Size = -MinValue + MaxValue + 1;

		internal const uint SizeForUnsigned = Size - Offset;

		private static FieldVal[] Init() {
			Debug.Assert((int)SizeForUnsigned >= 0);
			Debug.Assert(MinValue <= MaxValue);

			ref byte[] d0 = ref IntDataCache.Cache.DangerousGetReference();

			FieldVal[] r = new FieldVal[Size];
			ref FieldVal r0 = ref r.DangerousGetReference();

			int i = 0;
			const int IndexForZero = Offset;

			Debug.Assert(Size <= IntDataCache.Size);
			Debug.Assert(Size > IndexForZero);
			Debug.Assert(Size >= 4);

			const FieldTypeHint TypeBeforeZero = FieldTypeHint.IntNZ;
			do {
				U.Add(ref r0, i) = new FieldVal(TypeBeforeZero, U.Add(ref d0, i));
			} while (++i < IndexForZero);

			U.Add(ref r0, i++) = ZeroOrOneInstHolder.Zero;
			U.Add(ref r0, i++) = ZeroOrOneInstHolder.One;

			const FieldTypeHint Type = FieldTypeHint.IntP1;
			do {
				U.Add(ref r0, i) = new FieldVal(Type, U.Add(ref d0, i));
			} while (++i < Size);

			return r;
		}
	}

	private static class ZeroOrOneInstHolder {
		internal static readonly FieldVal Zero = new(FieldTypeHint.IntNZ);
		internal static readonly FieldVal One = new(FieldTypeHint.IntP1);
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
		// TODO Can be optimized by using `Unsafe.Write()` instead
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
			FieldTypeHints.IntNZOrP1(value),
			// NOTE: Avoids generation of unnecessary specialization of generic
			// method by reusing an existing one instead.
			MakeData<uint>(
				(uint)value.LittleEndian(),
				((uint)value.NonNegOrBitCompl()).CountBytesNeeded()
			)
		);
	}

	public static FieldVal From(long value) {
		long i = value + IntInstCache.Offset;
		if ((ulong)i < IntInstCache.Size) {
			return IntInstCache.Cache.DangerousGetReferenceAt((int)i);
		}
		return new(
			FieldTypeHints.IntNZOrP1(value),
			// NOTE: Avoids generation of unnecessary specialization of generic
			// method by reusing an existing one instead.
			MakeData<ulong>(
				(ulong)value.LittleEndian(),
				((ulong)value.NonNegOrBitCompl()).CountBytesNeeded()
			)
		);
	}

	// --

	public static FieldVal From(byte value) {
		Debug.Assert(byte.MinValue >= IntInstCache.MinValue);
		Debug.Assert(byte.MaxValue <= IntInstCache.MaxValue);
		return IntInstCache.Cache.DangerousGetReferenceAt(value + IntInstCache.Offset);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static FieldVal From(ushort value) => From((uint)value);

	public static FieldVal From(uint value) {
		if (value < (uint)IntInstCache.SizeForUnsigned) {
			return IntInstCache.Cache.DangerousGetReferenceAt((int)value + IntInstCache.Offset);
		}
		Debug.Assert(value != 0, $"A cached instance should've been returned instead.");
		return new(
			FieldTypeHint.IntP1,
			MakeData<uint>(
				value.LittleEndian(),
				value.CountBytesNeeded()
			)
		);
	}

	public static FieldVal From(ulong value) {
		if (value < (ulong)IntInstCache.SizeForUnsigned) {
			return IntInstCache.Cache.DangerousGetReferenceAt((int)value + IntInstCache.Offset);
		}
		Debug.Assert(value != 0, $"A cached instance should've been returned instead.");
		return new(
			FieldTypeHint.IntP1,
			MakeData<ulong>(
				value.LittleEndian(),
				value.CountBytesNeeded()
			)
		);
	}

	// --

	public static FieldVal From(double value) {
		if (sizeof(long) <= U.SizeOf<nint>()) {
			// NOTE: If it can be stored using the same data representation as
			// that for a `long`, then go ahead, but only when there's no cost.
			long i = (long)value;
			if (i == value) return From(i);
		} else {
			// NOTE: If checking for the `long` equivalent is more costly, just
			// check for the `int` equivalent instead.
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

	internal static FieldVal FromEnumIndex(int enumIndex) {
		Debug.Assert((uint)enumIndex <= (uint)FieldEnumAddr.MaxIndex, $"{nameof(enumIndex)}: {enumIndex}");
		// TODO Provide cached instances too
		return new(
			FieldTypeHint.Enum,
			// NOTE: Avoids generation of unnecessary specialization of generic
			// method by reusing an existing one instead.
			MakeData<uint>(
				(uint)enumIndex.LittleEndian(),
				((uint)enumIndex).CountBytesNeeded()
			)
		);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static FieldVal From(string text) => new(FieldTypeHint.Text, text.ToUTF8Bytes());

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static FieldVal From(byte[] blob) => new(FieldTypeHint.Blob, blob);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static FieldVal From(FieldTypeHint customType, byte[] blob) {
		if (customType.IsUnreserved()) {
			return new(customType, blob);
		}
		return E_TypeMustBeUnreserved_Arg();

		[DoesNotReturn]
		static FieldVal E_TypeMustBeUnreserved_Arg()
			=> throw new ArgumentException($"`{nameof(FieldTypeHint)}` used must be unreserved.", nameof(customType));
	}
}
