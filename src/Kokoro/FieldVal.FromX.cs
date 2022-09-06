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

		internal const int MinValue = -128;
		internal const int MaxValue = 255;

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
	[SkipLocalsInit]
	private static byte[] MakeData<T>(T value, int count) where T : unmanaged {
		Debug.Assert(count <= U.SizeOf<T>());
		var data = new byte[count];
		U.CopyBlockUnaligned(
			destination: ref data.DangerousGetReference(),
			source: ref U.As<T, byte>(ref value),
			byteCount: (uint)count
		);
		return data;
	}


	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static byte[] MakeDataForSigned(sbyte value) => MakeDataForUnsigned((byte)value);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static byte[] MakeDataForSigned(int value) => MakeData((uint)value.LittleEndian(), value.CountBytesNeededSigned());

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static byte[] MakeDataForSigned(long value) => MakeData((ulong)value.LittleEndian(), value.CountBytesNeededSigned());


	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static byte[] MakeDataForUnsigned(byte value) => new byte[1] { value };

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static byte[] MakeDataForUnsigned(uint value) => MakeData(value.LittleEndian(), value.CountBytesNeeded());

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static byte[] MakeDataForUnsigned(ulong value) => MakeData(value.LittleEndian(), value.CountBytesNeeded());

	// --

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static FieldVal From(bool value)
		=> ZeroOrOneInstHolder.DangerousGetZeroOrOne((int)(value.ToByte() & 1));

	// --

	public static FieldVal From(sbyte value) {
		const FieldTypeHint Type = FieldTypeHint.Int;
		if ((byte)value > 1u) return new(Type, MakeDataForSigned(value));
		return ZeroOrOneInstHolder.DangerousGetZeroOrOne((int)value);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static FieldVal From(short value) => From((int)value);

	public static FieldVal From(int value) {
		const FieldTypeHint Type = FieldTypeHint.Int;
		if ((uint)value > 1u) return new(Type, MakeDataForSigned(value));
		return ZeroOrOneInstHolder.DangerousGetZeroOrOne((int)value);
	}

	public static FieldVal From(long value) {
		const FieldTypeHint Type = FieldTypeHint.Int;
		if ((ulong)value > 1u) return new(Type, MakeDataForSigned(value));
		return ZeroOrOneInstHolder.DangerousGetZeroOrOne((int)value);
	}

	// --

	public static FieldVal From(byte value) {
		const FieldTypeHint Type = FieldTypeHint.UInt;
		if ((byte)value > 1u) return new(Type, MakeDataForUnsigned(value));
		return ZeroOrOneInstHolder.DangerousGetZeroOrOne((int)value);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static FieldVal From(ushort value) => From((uint)value);

	public static FieldVal From(uint value) {
		const FieldTypeHint Type = FieldTypeHint.UInt;
		if ((uint)value > 1u) return new(Type, MakeDataForUnsigned(value));
		return ZeroOrOneInstHolder.DangerousGetZeroOrOne((int)value);
	}

	public static FieldVal From(ulong value) {
		const FieldTypeHint Type = FieldTypeHint.UInt;
		if ((ulong)value > 1u) return new(Type, MakeDataForUnsigned(value));
		return ZeroOrOneInstHolder.DangerousGetZeroOrOne((int)value);
	}

	// --

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static FieldVal From(string text) => new(FieldTypeHint.Text, text.ToUTF8Bytes());

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static FieldVal From(byte[] blob) => new(FieldTypeHint.Blob, blob);
}
