namespace Kokoro;

using System.Runtime.InteropServices;

public class String_Facts {

	[TestTheory]
	[TLabel($@"`new string(char, int)` points to different (`ref char`) references (unless interned)")]
	[InlineData(2)]
	[InlineData(8)]
	[InlineData(32)]
	[InlineData(128)]
	[InlineData(512)]
	[InlineData(2048)]
	[SkipLocalsInit]
	public void T001(int iterations) {
		const string FiveNulls = "\0\0\0\0\0";
		ref char FiveNullsRef = ref MemoryMarshal.GetReference(FiveNulls.AsSpan());

		for (int i = 0; i < iterations; i++) {
			using AssertionCapture scope = new();

			string fiveNulls0 = string.Intern(new('\0', 5));
			string fiveNulls1 = new('\0', 5);
			string fiveNulls2 = new('\0', 5);

			ref char fiveNulls0Ref = ref MemoryMarshal.GetReference(fiveNulls0.AsSpan());
			ref char fiveNulls1Ref = ref MemoryMarshal.GetReference(fiveNulls1.AsSpan());
			ref char fiveNulls2Ref = ref MemoryMarshal.GetReference(fiveNulls2.AsSpan());

			Unsafe.AreSame(ref FiveNullsRef, ref fiveNulls0Ref).Should().BeTrue();

			Unsafe.AreSame(ref fiveNulls0Ref, ref fiveNulls1Ref).Should().BeFalse();
			Unsafe.AreSame(ref fiveNulls0Ref, ref fiveNulls2Ref).Should().BeFalse();

			Unsafe.AreSame(ref fiveNulls1Ref, ref fiveNulls2Ref).Should().BeFalse();
		}
	}
}
