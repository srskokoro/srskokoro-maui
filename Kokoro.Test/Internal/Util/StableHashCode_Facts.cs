namespace Kokoro.Internal.Util;
using System.Text;

public class StableHashCode_Facts : IRandomizedTest {
	private static Random Random => TestUtil.GetRandom<StableHashCode_Facts>();

	[TestFact]
	[TLabel("[m]: hash of 1 null != hash of 2 nulls != hash of 3 nulls")]
	public void T001_Of() {
		using var scope = new AssertionCapture();

		int hash1 = StableHashCode.Of("\0");
		int hash2 = StableHashCode.Of("\0\0");
		int hash3 = StableHashCode.Of("\0\0\0");

		hash1.Should().NotBe(hash2, $"the latter hash is for 2 nulls");
		hash2.Should().NotBe(hash3, $"the latter hash is for 3 nulls");
	}

	[TestFact]
	[TLabel("[m]: hash of random UTF-16BE bytes == hash of string representation")]
	public void T002_Of() {
		string str = Random.MakeAsciiStr(0, 42);
		byte[] bytes = Encoding.BigEndianUnicode.GetBytes(str); // UTF-16BE

		int expected = StableHashCode.Of(bytes);
		int returned = StableHashCode.Of(str);

		Assert.Equal(expected, returned);
	}

	[TestFact]
	[TLabel("[m]: hash of `Guid` bytes == hash of `Guid`")]
	public void T003_Of() {
		// See,
		// - https://stackoverflow.com/questions/6949598/can-i-assume-sizeofguid-16-at-all-times
		// - https://github.com/dotnet/runtime/blob/2c487d278398b3d1ac9679f7a3bdafb22b752f21/src/libraries/System.Private.CoreLib/src/System/Guid.cs#L142
		//
		Guid guid = new(Random.Init(stackalloc byte[16]));
		byte[] guidBytes = guid.ToByteArray();

		int expected = StableHashCode.Of(guidBytes);
		int returned = StableHashCode.Of(guid);

		Assert.Equal(expected, returned);
	}

	[TestTheory]
	[TLabel("[m]: Tamper Protection -- hash algorithm output is still as expected")]
	[InlineData("", -1357330238)]
	[InlineData("Hello World!", -1530810269)]
	[InlineData("The quick brown fox jumps over the lazy dog.", 1323891283)]
	[InlineData("\0In between nulls\0and more.\r\nWith new line.", 219757796)]
	public void T004_Of(string str, int hash) {
		Assert.Equal(hash, StableHashCode.Of(str));
	}
}
