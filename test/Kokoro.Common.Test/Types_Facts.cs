﻿namespace Kokoro.Common;

public class Types_Facts : IRandomizedTest {
	static Random Random => TestUtil.GetRandom<Types_Facts>();

	[TestFact]
	[TLabel($"`[.](Guid)` returns `Guid` type object")]
	public void T001_TypeOf() {
		var guid = new Guid(Random.Init(stackalloc byte[16]));

		Type expected = typeof(Guid);
		Type returned = Types.TypeOf(Guid.Empty);

		Assert.Equal(expected, returned);
	}

	[TestFact]
	[TLabel($"`[.](int)` returns `int` type object")]
	public void T002_TypeOf() {
		int value = Random.Next();

		Type expected = typeof(int);
		Type returned = Types.TypeOf(value);

		Assert.Equal(expected, returned);
	}

	[TestFact]
	[TLabel($"`[.](string)` returns `string` type object")]
	public void T003_TypeOf() {
		string str = Random.MakeAsciiStr(0, 42);

		Type expected = typeof(string);
		Type returned = Types.TypeOf(str);

		Assert.Equal(expected, returned);
	}

	[TestTheory]
	[TLabel($"[m!] returns expected type object")]
	[MemberData(nameof(T004_TypeOf__TheoryData), DisableDiscoveryEnumeration = true)]
	public void T004_TypeOf(object value, Type expectedType) {
		Assert.Equal(expectedType, Types.TypeOf(value));
	}

	public static TheoryData<object?, Type> T004_TypeOf__TheoryData => new() {
		{ null, typeof(object) },
		{ new object(), typeof(object) },
		{ Guid.Empty /* Boxed */, typeof(Guid) },
		{ new StrongBox<Guid>(), typeof(StrongBox<Guid>) },
		{ (0L, (ushort)1).ToTuple(), typeof(Tuple<long, ushort>) },
		{ (0L, (ushort)1) /* Boxed */, typeof(ValueTuple<long, ushort>) },
	};
}
