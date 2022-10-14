namespace Kokoro;
using Kokoro.Test.Util;
using System.Runtime.InteropServices;

public class FieldVal_Facts : IRandomizedTest {
	static Random Random => TestUtil.GetRandom<FieldVal_Facts>();

	[TestTheory, CombinatorialData]
	[TLabel("Given integers, [m!] returns cached instances with correct values")]
	public void T001_From(
		[CombinatorialValues(sbyte.MinValue, 0)] int source,
		[CombinatorialValues(0, sbyte.MaxValue, byte.MaxValue)] int deviation
	) {
		source = Random.Next(source, source + deviation + 1);
		const int o = 0; // Adjust to produce error

		using AssertionCapture parent = new();

		// As 8-bit signed integers
		using (new AssertionCapture()) {
			sbyte value = (sbyte)source;
			var fval1 = FieldVal.From(value+o);
			var fval2 = FieldVal.From(value);

			ReferenceEquals(fval1, fval2).Should().BeTrue(
				because: "they're expected to be from the cache");

			// NOTE: `GetInt64()` is on purpose
			fval1.GetInt64().Should().Be(value,
				because: "it should be the same as the source value");
		}

		// As 32-bit signed integers
		using (new AssertionCapture()) {
			int value = source;
			var fval1 = FieldVal.From(value+o);
			var fval2 = FieldVal.From(value);

			ReferenceEquals(fval1, fval2).Should().BeTrue(
				because: "they're expected to be from the cache");

			// NOTE: `GetInt64()` is on purpose
			fval1.GetInt64().Should().Be(value,
				because: "it should be the same as the source value");
		}

		// As 8-bit unsigned integers
		using (new AssertionCapture()) {
			byte value = (byte)source;
			var fval1 = FieldVal.From(value+o);
			var fval2 = FieldVal.From(value);

			ReferenceEquals(fval1, fval2).Should().BeTrue(
				because: "they're expected to be from the cache");

			// NOTE: `GetInt64()` is on purpose
			fval1.GetInt64().Should().Be(value,
				because: "it should be the same as the source value");
		}

		// As double-precision floats
		using (new AssertionCapture()) {
			int value = source;
			var fval1 = FieldVal.From(value+o);
			var fval2 = FieldVal.From(value);

			ReferenceEquals(fval1, fval2).Should().BeTrue(
				because: "they're expected to be from the cache");

			fval1.GetReal().Should().Be(value,
				because: "it should be the same as the source value");
		}
	}

	[TestFact]
	[TLabel("Given an integer, return the same value back")]
	public void T002() {
		ulong source = Random.NextUniform64();
		const int o = 0; // Adjust to produce error

		using AssertionCapture parent = new();

		// Signed integers
		// -=-

		// sbyte
		using (new AssertionCapture()) {
			sbyte x = (sbyte)source;
			FieldVal.From((sbyte)(x+o)).GetInt8().Should().Be(x);
			// Test truncation
			FieldVal.From((long)(source+o)).GetInt8().Should().Be(x);
			FieldVal.From((ulong)(source+o)).GetInt8().Should().Be(x);
		}

		// short
		using (new AssertionCapture()) {
			short x = (short)source;
			FieldVal.From((short)(x+o)).GetInt16().Should().Be(x);
			// Test truncation
			FieldVal.From((long)(source+o)).GetInt16().Should().Be(x);
			FieldVal.From((ulong)(source+o)).GetInt16().Should().Be(x);
		}

		// int
		using (new AssertionCapture()) {
			int x = (int)source;
			FieldVal.From((int)(x+o)).GetInt32().Should().Be(x);
			// Test truncation
			FieldVal.From((long)(source+o)).GetInt32().Should().Be(x);
			FieldVal.From((ulong)(source+o)).GetInt32().Should().Be(x);
		}

		// long
		using (new AssertionCapture()) {
			long x = (long)source;
			FieldVal.From((long)(x+o)).GetInt64().Should().Be(x);
		}

		// Unsigned integers
		// -=-

		// byte
		using (new AssertionCapture()) {
			byte x = (byte)source;
			FieldVal.From((byte)(x+o)).GetUInt8().Should().Be(x);
			// Test truncation
			FieldVal.From((long)(source+o)).GetUInt8().Should().Be(x);
			FieldVal.From((ulong)(source+o)).GetUInt8().Should().Be(x);
		}

		// short
		using (new AssertionCapture()) {
			ushort x = (ushort)source;
			FieldVal.From((ushort)(x+o)).GetUInt16().Should().Be(x);
			// Test truncation
			FieldVal.From((long)(source+o)).GetUInt16().Should().Be(x);
			FieldVal.From((ulong)(source+o)).GetUInt16().Should().Be(x);
		}

		// int
		using (new AssertionCapture()) {
			uint x = (uint)source;
			FieldVal.From((uint)(x+o)).GetUInt32().Should().Be(x);
			// Test truncation
			FieldVal.From((long)(source+o)).GetUInt32().Should().Be(x);
			FieldVal.From((ulong)(source+o)).GetUInt32().Should().Be(x);
		}

		// long
		using (new AssertionCapture()) {
			ulong x = (ulong)source;
			FieldVal.From((ulong)(x+o)).GetUInt64().Should().Be(x);
		}
	}

	[TestFact]
	[TLabel($"When `{nameof(FieldTypeHint.IntNZ)}`, empty data gives `0`")]
	public void T003() {
		var fval = new FieldVal(FieldTypeHint.IntNZ, Array.Empty<byte>());
		const int Expected = 0;

		using var scope = new AssertionCapture();

		fval.GetInt8().Should().Be((sbyte)Expected);
		fval.GetInt16().Should().Be((short)Expected);
		fval.GetInt32().Should().Be((int)Expected);
		fval.GetInt64().Should().Be((long)Expected);

		fval.GetUInt8().Should().Be((byte)Expected);
		fval.GetUInt16().Should().Be((ushort)Expected);
		fval.GetUInt32().Should().Be((uint)Expected);
		fval.GetUInt64().Should().Be((ulong)Expected);

		fval.GetReal().Should().Be((double)Expected);
	}

	[TestFact]
	[TLabel($"When `{nameof(FieldTypeHint.IntP1)}`, empty data gives `1`")]
	public void T004() {
		var fval = new FieldVal(FieldTypeHint.IntP1, Array.Empty<byte>());
		const int Expected = 1;

		using var scope = new AssertionCapture();

		fval.GetInt8().Should().Be((sbyte)Expected);
		fval.GetInt16().Should().Be((short)Expected);
		fval.GetInt32().Should().Be((int)Expected);
		fval.GetInt64().Should().Be((long)Expected);

		fval.GetUInt8().Should().Be((byte)Expected);
		fval.GetUInt16().Should().Be((ushort)Expected);
		fval.GetUInt32().Should().Be((uint)Expected);
		fval.GetUInt64().Should().Be((ulong)Expected);

		fval.GetReal().Should().Be((double)Expected);
	}

	[TestTheory, CombinatorialData]
	[TLabel($"Given random data bytes, return the expected integer")]
	public void T005([CombinatorialValues(FieldTypeHint.IntNZ, FieldTypeHint.IntP1)] FieldTypeHint typeHint) {
		long m1 = typeHint.WhenIntRetM1IfIntNZ();
		long x = (long)Random.NextUniform64();

		long xLE = x.LittleEndian();
		Span<byte> span = MemoryMarshal.CreateSpan(ref U.As<long, byte>(ref xLE), sizeof(long));
		static byte[] MakeData(Span<byte> span, int length) => span.Slice(0, length).ToArray();

		using var scope = new AssertionCapture();

		new FieldVal(typeHint, MakeData(span, 1)).GetInt8().Should().Be((sbyte)((m1   << 8)  | (x & 0x_FF)));
		new FieldVal(typeHint, MakeData(span, 2)).GetInt16().Should().Be((short)((m1  << 16) | (x & 0x_FFFF)));
		new FieldVal(typeHint, MakeData(span, 4)).GetInt32().Should().Be((int)((m1    << 32) | (x & 0x_FFFF_FFFF)));
		new FieldVal(typeHint, MakeData(span, 8)).GetInt64().Should().Be((long)x);

		new FieldVal(typeHint, MakeData(span, 1)).GetUInt8().Should().Be((byte)((m1   << 8)  | (x & 0x_FF)));
		new FieldVal(typeHint, MakeData(span, 2)).GetUInt16().Should().Be((ushort)((m1<< 16) | (x & 0x_FFFF)));
		new FieldVal(typeHint, MakeData(span, 4)).GetUInt32().Should().Be((uint)((m1  << 32) | (x & 0x_FFFF_FFFF)));
		new FieldVal(typeHint, MakeData(span, 8)).GetUInt64().Should().Be((ulong)x);
	}
}
