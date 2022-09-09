namespace Kokoro;
using Kokoro.Test.Util;

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
}
