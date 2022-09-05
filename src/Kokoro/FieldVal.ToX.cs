namespace Kokoro;
using System.Buffers.Binary;
using System.Globalization;
using System.Numerics;

public sealed partial class FieldVal {

	[SkipLocalsInit]
	public string? GetText() {
		FieldTypeHint type = _TypeHint;
		if (type == FieldTypeHint.Text) {
			return TextUtils.UTF8ToString(_Data);
		} else if (type == FieldTypeHint.Null) {
			return null;
		} else {
			return ForceDecodeAsText(type, _Data);
		}
	}

	[SkipLocalsInit]
	private static string ForceDecodeAsText(FieldTypeHint type, byte[] data) {
		string r;
		goto Switch;

	Return: // Ensures epilogue happens here instead
		return r;

	Switch:
		switch (type) {
			default: {
				r = TextUtils.UTF8ToString(data);
				goto Return;
			}

			case FieldTypeHint.Zero: { r = "0"; goto Return; }
			case FieldTypeHint.One: { r = "1"; goto Return; }

			case FieldTypeHint.Int:
			case FieldTypeHint.UInt: {
				Debug.Assert((FieldTypeHintInt)FieldTypeHint.Int == 0x4);
				Debug.Assert((FieldTypeHintInt)FieldTypeHint.UInt == 0x5);
				r = new BigInteger(
					value: data,
					isUnsigned: ((FieldTypeHintInt)type & 1) != 0,
					isBigEndian: true
				).ToString(NumberFormatInfo.InvariantInfo);
				goto Return;
			}

			case FieldTypeHint.Fp16: {
				Half value = BinaryPrimitives.ReadHalfBigEndian(data);
				r = value.ToString(NumberFormatInfo.InvariantInfo);
				goto Return;
			}
			case FieldTypeHint.Fp32: {
				float value = BinaryPrimitives.ReadSingleBigEndian(data);
				r = value.ToString(NumberFormatInfo.InvariantInfo);
				goto Return;
			}
			case FieldTypeHint.Fp64: {
				double value = BinaryPrimitives.ReadDoubleBigEndian(data);
				r = value.ToString(NumberFormatInfo.InvariantInfo);
				goto Return;
			}
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public ReadOnlySpan<byte> GetBlob() => Data;
}
