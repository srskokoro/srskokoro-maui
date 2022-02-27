using System.Text;

namespace Kokoro.Internal.Util;

public class StableHashCode_Test : IRandomizedTest {
	private static Random Random => TestUtil.GetRandom<StableHashCode_Test>();

	[Fact]
	public void HashOf_2Nulls_IsNot_HashOf_3Nulls() {
		int hash1 = StableHashCode.Of("\0\0");
		int hash2 = StableHashCode.Of("\0\0\0");
		Assert.NotEqual(hash1, hash2);
	}

	[Fact]
	public void HashOf_String_Equals_HashOf_UTF16BE_Bytes() {
		string str = Random.MakeAsciiStr(0, 42);
		byte[] bytes = Encoding.BigEndianUnicode.GetBytes(str);

		int expected = StableHashCode.Of(bytes);
		int returned = StableHashCode.Of(str);

		Assert.Equal(expected, returned);
	}

	[Fact]
	public void HashOf_Guid_Equals_HashOf_Guid_Bytes() {
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

	/// <summary>
	/// Hash algorithm tamper protection.
	/// </summary>
	[Theory]
	[InlineData("", -1357330238)]
	[InlineData("Hello World!", -1530810269)]
	[InlineData("The quick brown fox jumps over the lazy dog.", 1323891283)]
	[InlineData("\0In between nulls\0and more.\r\nWith new line.", 219757796)]
	public void HashOf_String_AlgorithmOutput_Is_Still_AsExpected(string str, int hash) {
		Assert.Equal(hash, StableHashCode.Of(str));
	}
}
