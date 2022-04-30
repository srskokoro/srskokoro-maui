namespace Kokoro;

public class StringKey_Facts : IRandomizedTest {
	static Random Random => TestUtil.GetRandom<StringKey_Facts>();

	[TestFact]
	[TLabel("Properly implements `IEquatable<T>`")]
	public void T001_IEquatable() {
		string testStr = Random.MakeAsciiStr(0, 42);
		string testStr2 = Random.MakeAsciiStr(0, 42);

		if (testStr == testStr2)
			testStr2 = Random.NextAscii() + testStr;

		StringKey inst = new(testStr);
		StringKey eqInst = new(testStr);
		StringKey eqInst2 = new(new string(testStr.AsSpan()));
		StringKey neqInst = new(testStr2);

		ComprehensiveAssert.ProperlyImplements_IEquatable(inst, eqInst, eqInst2, neqInst);
	}
}
