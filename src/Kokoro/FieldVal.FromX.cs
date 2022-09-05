namespace Kokoro;

public sealed partial class FieldVal {

	public static FieldVal Zero => ZeroOrOneInstHolder.Zero;
	public static FieldVal One => ZeroOrOneInstHolder.One;

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
	private static byte[] MakeData<T>(T value) where T : unmanaged {
		var data = new byte[U.SizeOf<T>()];
		U.WriteUnaligned(ref data.DangerousGetReference(), value);
		return data;
	}

	// --

	public static FieldVal From(sbyte value) {
		const FieldTypeHint Type = FieldTypeHint.Int;
		if ((byte)value > 1u) return new(Type, MakeData(value.ToBigEndian()));
		return ZeroOrOneInstHolder.DangerousGetZeroOrOne((int)value);
	}

	public static FieldVal From(short value) {
		const FieldTypeHint Type = FieldTypeHint.Int;
		if ((ushort)value > 1u) return new(Type, MakeData(value.ToBigEndian()));
		return ZeroOrOneInstHolder.DangerousGetZeroOrOne((int)value);
	}

	public static FieldVal From(int value) {
		const FieldTypeHint Type = FieldTypeHint.Int;
		if ((uint)value > 1u) return new(Type, MakeData(value.ToBigEndian()));
		return ZeroOrOneInstHolder.DangerousGetZeroOrOne((int)value);
	}

	public static FieldVal From(long value) {
		const FieldTypeHint Type = FieldTypeHint.Int;
		if ((ulong)value > 1u) return new(Type, MakeData(value.ToBigEndian()));
		return ZeroOrOneInstHolder.DangerousGetZeroOrOne((int)value);
	}

	// --

	public static FieldVal From(byte value) {
		const FieldTypeHint Type = FieldTypeHint.UInt;
		if ((byte)value > 1u) return new(Type, MakeData(value.ToBigEndian()));
		return ZeroOrOneInstHolder.DangerousGetZeroOrOne((int)value);
	}

	public static FieldVal From(ushort value) {
		const FieldTypeHint Type = FieldTypeHint.UInt;
		if ((ushort)value > 1u) return new(Type, MakeData(value.ToBigEndian()));
		return ZeroOrOneInstHolder.DangerousGetZeroOrOne((int)value);
	}

	public static FieldVal From(uint value) {
		const FieldTypeHint Type = FieldTypeHint.UInt;
		if ((uint)value > 1u) return new(Type, MakeData(value.ToBigEndian()));
		return ZeroOrOneInstHolder.DangerousGetZeroOrOne((int)value);
	}

	public static FieldVal From(ulong value) {
		const FieldTypeHint Type = FieldTypeHint.UInt;
		if ((ulong)value > 1u) return new(Type, MakeData(value.ToBigEndian()));
		return ZeroOrOneInstHolder.DangerousGetZeroOrOne((int)value);
	}

	// --

	public static FieldVal From(string text) => new(FieldTypeHint.Text, text.ToUTF8Bytes());

	public static FieldVal From(byte[] blob) => new(FieldTypeHint.Blob, blob);
}
