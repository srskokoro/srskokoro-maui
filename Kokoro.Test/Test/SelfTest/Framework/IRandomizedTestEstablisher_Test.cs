namespace Kokoro.Test.SelfTest.Framework;

using Kokoro.Test.Framework;
using System.Globalization;
using Xunit.Sdk;
using static Kokoro.Test.Framework.IRandomizedTestEstablisher;
using TestFramework = Test.Framework.TestFramework;

public abstract class IRandomizedTestEstablisher_Test_Base {
	private protected const string Test_DateTimeSeedDir = $@"self_test";
	private protected const string Test_DateTimeSeedFile = $@"{Test_DateTimeSeedDir}\test_start_dt_preserved_on_fail.dat";

	private protected const string Test_DateTimeSeed_1 = "2023-12-22T03:33:38+08:00";
	private protected const int Test_DateTimeSeed_1_ResultSeed = -1870755152;

	private protected const string Test_DateTimeSeed_2 = "2022-01-01T21:49:06+00:00";
	private protected const int Test_DateTimeSeed_2_ResultSeed = -556981469;

	private protected const string Test_DateTimeSeed_3 = "2022-02-17T21:49:06Z";
	private protected const string Test_DateTimeSeed_3_Alt = "2022-02-17T21:49:06+00:00";
	private protected const int Test_DateTimeSeed_3_ResultSeed = 2103003800;
}

public sealed class IRandomizedTestEstablisher_Test
	: IRandomizedTestEstablisher_Test_Base, IRandomizedTestEstablisher {

	[TestFact]
	[TLabel($"[p] is properly set up by `{nameof(TestFramework)}`")]
	public void T001_RandomSeedBase() {
		Exception? e = Record.Exception(() => {
			_ = RandomSeedBase;
		});
		Assert.Null(e);
	}

	[TestFact]
	[TLabel($"`{nameof(_Config)}.[x]` is either valid or empty")]
	public void T002_DateTimeSeed() {
		Assert.True(
			TryParseDateTimeSeed(_Config.DateTimeSeed, out _)
			|| string.IsNullOrWhiteSpace(_Config.DateTimeSeed)
			, "Invalid configuration! Should be either empty or set to a valid ISO 8601 date time string."
		);
	}

	[TestTheory]
	[TLabel($"[m] supports ISO 8601 datetime")]
	[InlineData(Test_DateTimeSeed_1, Test_DateTimeSeed_1)]
	[InlineData(Test_DateTimeSeed_2, Test_DateTimeSeed_2)]
	[InlineData(Test_DateTimeSeed_3, Test_DateTimeSeed_3_Alt)]
	public void T003_TryParseDateTimeSeed(string isoDateTimeStrInput, string isoDateTimeStrOutput) {
		Assert.True(
			TryParseDateTimeSeed(isoDateTimeStrInput, out var dto),
			$"Unexpected! ISO 8601 string not supported: {isoDateTimeStrInput}"
		);
		Assert.Equal(
			isoDateTimeStrOutput,
			dto.ToString(DateTimeSeed_ExpectedFormat, CultureInfo.InvariantCulture)
		);
	}

	[TestFact]
	[TLabel($"[p] doesn't change on its own")]
	public async Task T004_RandomSeedBase() {
		// `async` ensures our `AsyncLocal`s will be under a different context
		await Task.CompletedTask; // Suppress CS1998

		FreezeLocalDefaultDateTimeSeed();
		int seed1 = RandomSeedBase;

		AdvanceLocalDefaultDateTimeSeed();
		int seed2 = RandomSeedBase;

		Assert.Equal(seed1, seed2);
	}
}

public sealed class IRandomizedTestEstablisher_Test_WithDirSetup
	: IRandomizedTestEstablisher_Test_Base, IRandomizedTestEstablisher
	, IClassFixture<IRandomizedTestEstablisher_Test_WithDirSetup.DirectoryFixture>
	, IDisposable {

	public sealed class DirectoryFixture : IDisposable {

		public DirectoryFixture() {
			Directory.CreateDirectory(Test_DateTimeSeedDir);
		}

		public void Dispose() {
			Directory.Delete(Test_DateTimeSeedDir, true);
		}
	}

	private readonly int _RandomSeedBase_Backup;

	public IRandomizedTestEstablisher_Test_WithDirSetup(DirectoryFixture _) {
		_RandomSeedBase_Backup = RandomSeedBase;

		// Ensure doesn't exist prior any test case
		File.Delete(Test_DateTimeSeedFile);
	}

	public void Dispose() {
		// Assert that no test method ever modified the actual `RandomSeedBase`
		if (_RandomSeedBase_Backup != RandomSeedBase) {
			throw new AssertActualExpectedException(
				expected: _RandomSeedBase_Backup,
				actual: RandomSeedBase,
				$"The test method modified the actual `{nameof(RandomSeedBase)}`. " +
				$"It should've been marked `async` (returning `Task`) to ensure " +
				$"that any modifications to `{nameof(RandomSeedBase)}` doesn't " +
				$"escape the test method.");

			// At the moment, the exception above is never thrown. This is
			// because even a non-async test method is always ran wrapped in an
			// async method by default (by `XunitTestInvoker`). Nonetheless,
			// should xUnit's underlying implementation change, the setup above
			// will ensure intended behavior.
			//
			// See also, https://stackoverflow.com/a/49232776
		}
	}

	private static async Task<string> ReadFirstLineAsync(string file) {
		using var reader = new StreamReader(file);
		return await reader.ReadLineAsync() ?? "";
	}

	private static string GetFirstLineOfString(string str) {
		using var reader = new StringReader(str);
		return reader.ReadLine() ?? "";
	}

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously

	#region `LoadLocalRandomState` tests

	[TestTheory]
	[TLabel($"[m] doesn't create datetime seed file when datetime seed override string is valid")]
	[InlineData(Test_DateTimeSeed_1)]
	[InlineData(Test_DateTimeSeed_2)]
	[InlineData(Test_DateTimeSeed_3)]
	public async Task T005_LoadLocalRandomState(string validDateTimeSeed) {
		// `async` ensures our `AsyncLocal`s will be under a different context

		LoadLocalRandomState(validDateTimeSeed, Test_DateTimeSeedFile);
		Assert.False(File.Exists(Test_DateTimeSeedFile), "Should've been not created.");
	}

	[TestTheory]
	[TLabel($"[m] creates empty datetime seed file when datetime seed override is invalid")]
	[InlineData("")]
	[InlineData("An invalid content.")]
	public async Task T006_LoadLocalRandomState(string invalidDateTimeSeed) {
		// `async` ensures our `AsyncLocal`s will be under a different context

		// Also implies that the following will not throw on invalid input
		LoadLocalRandomState(invalidDateTimeSeed, Test_DateTimeSeedFile);

		Assert.True(File.Exists(Test_DateTimeSeedFile), "Should've been created.");
		string contents = await File.ReadAllTextAsync(Test_DateTimeSeedFile);

		Assert.Empty(contents);
	}

	[TestTheory]
	[TLabel($"[m] sets up `{nameof(RandomSeedBase)}` even when datetime seed override is invalid")]
	[InlineData("")]
	[InlineData("An invalid content.")]
	public async Task T007_LoadLocalRandomState(string invalidDateTimeSeed) {
		// `async` ensures our `AsyncLocal`s will be under a different context

		// Also implies that this will not throw on invalid input
		LoadLocalRandomState(invalidDateTimeSeed, Test_DateTimeSeedFile);

		Exception? e = Record.Exception(() => {
			_ = RandomSeedBase;
		});

		Assert.Null(e);
	}

	[TestTheory]
	[TLabel($"[m] sets up `{nameof(RandomSeedBase)}` even when datetime seed file is invalid")]
	[InlineData("")]
	[InlineData("An invalid content.")]
	[InlineData("Foo\r\nBar\r\n")]
	[InlineData("Foo\nBar\nBaz")]
	public async Task T008_LoadLocalRandomState(string invalidDateTimeSeed) {
		// `async` ensures our `AsyncLocal`s will be under a different context

		await File.WriteAllTextAsync(Test_DateTimeSeedFile, invalidDateTimeSeed);

		// Also implies that this will not throw on invalid input
		LoadLocalRandomState("", Test_DateTimeSeedFile);

		Exception? e = Record.Exception(() => {
			_ = RandomSeedBase;
		});

		Assert.Null(e);
	}

	[TestTheory, CombinatorialData]
	[TLabel($"[m] loads same `{nameof(RandomSeedBase)}` whether from string or file, even when file has extra lines")]
	public async Task T009_LoadLocalRandomState(
		[CombinatorialValues(Test_DateTimeSeed_1, Test_DateTimeSeed_2, Test_DateTimeSeed_3)] string dateTimeSeed,
		[CombinatorialValues(null, "", "An invalid content.")] string? extraFileLinesContent
	) {
		// `async` ensures our `AsyncLocal`s will be under a different context

		string dateTimeSeedFileContent = dateTimeSeed;
		if (extraFileLinesContent is not null) {
			dateTimeSeedFileContent += Environment.NewLine + extraFileLinesContent;
		}

		async Task<int> LoadFromString() {
			LoadLocalRandomState(dateTimeSeed, "");
			return RandomSeedBase;
		}

		async Task<int> LoadFromFile() {
			await File.WriteAllTextAsync(Test_DateTimeSeedFile, dateTimeSeedFileContent);
			LoadLocalRandomState("", Test_DateTimeSeedFile);
			return RandomSeedBase;
		}

		int loadedFromString = await LoadFromString();
		int loadedFromFile = await LoadFromFile();

		Assert.Equal(loadedFromString, loadedFromFile);
	}

	[TestTheory]
	[TLabel($"[m] loads expected `{nameof(RandomSeedBase)}` while ignoring existing datetime seed file")]
	[InlineData(Test_DateTimeSeed_1, Test_DateTimeSeed_2)]
	[InlineData(Test_DateTimeSeed_1, Test_DateTimeSeed_3)]
	[InlineData(Test_DateTimeSeed_2, Test_DateTimeSeed_1)]
	[InlineData(Test_DateTimeSeed_2, Test_DateTimeSeed_3)]
	[InlineData(Test_DateTimeSeed_3, Test_DateTimeSeed_1)]
	[InlineData(Test_DateTimeSeed_3, Test_DateTimeSeed_2)]
	public async Task T010_LoadLocalRandomState(string dateTimeSeedOverride, string dateTimeSeedFileContent) {
		// `async` ensures our `AsyncLocal`s will be under a different context

		async Task<int> LoadFromString() {
			LoadLocalRandomState(dateTimeSeedOverride, "");
			return RandomSeedBase;
		}

		async Task<int> LoadFromString_WithExistingFile() {
			await File.WriteAllTextAsync(Test_DateTimeSeedFile, dateTimeSeedFileContent);
			LoadLocalRandomState(dateTimeSeedOverride, Test_DateTimeSeedFile);
			return RandomSeedBase;
		}

		int expected = await LoadFromString();
		int actual = await LoadFromString_WithExistingFile();

		Assert.Equal(expected, actual);
	}

	[TestTheory, CombinatorialData]
	[TLabel($"[m] doesn't modify existing datetime seed file")]
	public async Task T011_LoadLocalRandomState(
		[CombinatorialValues("", Test_DateTimeSeed_1)]
		string dateTimeSeedOverride,
		[CombinatorialValues(Test_DateTimeSeed_1, Test_DateTimeSeed_2, Test_DateTimeSeed_3, "", "An invalid content.")]
		string expectedContent
	) {
		// `async` ensures our `AsyncLocal`s will be under a different context

		await File.WriteAllTextAsync(Test_DateTimeSeedFile, expectedContent);
		LoadLocalRandomState(dateTimeSeedOverride, Test_DateTimeSeedFile);

		string actualContent = await File.ReadAllTextAsync(Test_DateTimeSeedFile);
		Assert.Equal(expectedContent, actualContent);
	}

	[TestFact]
	[TLabel($"[m] twice sets different seeds")]
	public async Task T012_LoadLocalRandomState() {
		// `async` ensures our `AsyncLocal`s will be under a different context

		FreezeLocalDefaultDateTimeSeed();

		LoadLocalRandomState("", Test_DateTimeSeedFile);
		int seed1 = RandomSeedBase;

		AdvanceLocalDefaultDateTimeSeed();

		LoadLocalRandomState("", Test_DateTimeSeedFile);
		int seed2 = RandomSeedBase;

		// May also fail if we get lucky, but that should be expected as highly
		// unlikely; most likely: something went wrong in the implementation.
		Assert.NotEqual(seed1, seed2);
	}

	[TestFact]
	[TLabel($"[m] twice on same datetime seed override sets same seed")]
	public async Task T013_LoadLocalRandomState() {
		// `async` ensures our `AsyncLocal`s will be under a different context
		await Parallel.ForEachAsync(new string[] {
			Test_DateTimeSeed_1,
			Test_DateTimeSeed_2,
			Test_DateTimeSeed_3,
		}, static async (dateTimeSeedOverride, _) => {
			// `async` ensures our `AsyncLocal`s will be under a different context

			FreezeLocalDefaultDateTimeSeed();

			LoadLocalRandomState(dateTimeSeedOverride, "");
			int seed1 = RandomSeedBase;

			AdvanceLocalDefaultDateTimeSeed();

			LoadLocalRandomState(dateTimeSeedOverride, "");
			int seed2 = RandomSeedBase;

			if (seed1 != seed2)
				throw new AssertActualExpectedException(seed1, seed2, $"Failed case: {dateTimeSeedOverride}");
		});
	}

	[TestTheory]
	[TLabel($"[m]: Tamper Protection -- algorithm output is still as expected")]
	[InlineData(Test_DateTimeSeed_1, -1870755152)]
	[InlineData(Test_DateTimeSeed_2, -556981469)]
	[InlineData(Test_DateTimeSeed_3, 2103003800)]
	public async Task T014_LoadLocalRandomState(string dateTimeSeed, int expected) {
		// `async` ensures our `AsyncLocal`s will be under a different context

		LoadLocalRandomState(dateTimeSeed, "");
		int actual = RandomSeedBase;

		Assert.Equal(expected, actual);
	}

	#endregion

	#region `SaveLocalRandomState` tests

	[TestFact]
	[TLabel($"[m] deletes datetime seed file when all (mock) tests passed")]
	public async Task T015_SaveLocalRandomState() {
		// `async` ensures our `AsyncLocal`s will be under a different context

		LoadLocalRandomState("", Test_DateTimeSeedFile); // Should at least create an empty file
		Assert.True(File.Exists(Test_DateTimeSeedFile), "Should exist first.");

		SaveLocalRandomState(new RunSummary { Total = 1, Failed = 0 }, Test_DateTimeSeedFile);
		Assert.False(File.Exists(Test_DateTimeSeedFile), "Shouldn't exist anymore.");
	}

	[TestTheory]
	[TLabel($"[m] persists valid datetime seed file when some (mock) tests failed, but overwrites prior file content if any")]
	[InlineData(null)]
	[InlineData($"{Test_DateTimeSeed_1}\r\nAn invalid content.")]
	[InlineData($"{Test_DateTimeSeed_2}\r\nAn invalid content.")]
	[InlineData($"An invalid content.\r\n{Test_DateTimeSeed_3}")]
	[InlineData($"An invalid content.")]
	public async Task T016_SaveLocalRandomState(string? priorFileContent) {
		// `async` ensures our `AsyncLocal`s will be under a different context

		if (priorFileContent is not null) {
			Assert.False(TryParseDateTimeSeed(priorFileContent, out _), "Input must first be unparsable in whole.");
			await File.WriteAllTextAsync(Test_DateTimeSeedFile, priorFileContent);
		}

		// Also implies that this will not throw on invalid input
		LoadLocalRandomState("", Test_DateTimeSeedFile); // Should at least create an empty file
		Assert.True(File.Exists(Test_DateTimeSeedFile), "Should exist first.");

		SaveLocalRandomState(new RunSummary { Total = 1, Failed = 1 }, Test_DateTimeSeedFile);
		Assert.True(File.Exists(Test_DateTimeSeedFile), "Should exist still.");

		string outputFileContent = await File.ReadAllTextAsync(Test_DateTimeSeedFile);
		string dateTimeSeed = GetFirstLineOfString(outputFileContent);
		bool success = TryParseDateTimeSeed(dateTimeSeed, out _);

		Assert.True(success, $"Should've persisted a valid date time string.{Environment.NewLine}Persisted instead: {dateTimeSeed}");

		if (priorFileContent is not null) {
			Assert.NotEqual(priorFileContent, outputFileContent);
		}
	}

	[TestTheory]
	[TLabel($"[m] saves expected datetime seed when some (mock) tests failed")]
	[InlineData(Test_DateTimeSeed_1)]
	[InlineData(Test_DateTimeSeed_2)]
	[InlineData(Test_DateTimeSeed_3)]
	public async Task T017_SaveLocalRandomState(string dateTimeSeed) {
		// `async` ensures our `AsyncLocal`s will be under a different context

		bool success = TryParseDateTimeSeed(dateTimeSeed, out var expected);
		Assert.True(success, "Input should've been valid first.");
		LoadLocalRandomState(dateTimeSeed, "");

		SaveLocalRandomState(new RunSummary { Total = 1, Failed = 1 }, Test_DateTimeSeedFile);
		string dateTimeSeedOutput = await ReadFirstLineAsync(Test_DateTimeSeedFile);
		success = TryParseDateTimeSeed(dateTimeSeedOutput, out var actual);
		Assert.True(success, "Couldn't parse output.");

		Assert.Equal(expected, actual);
	}

	[TestTheory]
	[TLabel($"[m] doesn't modify `{nameof(RandomSeedBase)}`")]
	[InlineData(0)]
	[InlineData(1)]
	public async Task T018_SaveLocalRandomState(int failCount) {
		// `async` ensures our `AsyncLocal`s will be under a different context

		FreezeLocalDefaultDateTimeSeed();

		LoadLocalRandomState("", Test_DateTimeSeedFile);
		int expected = RandomSeedBase;

		AdvanceLocalDefaultDateTimeSeed();

		SaveLocalRandomState(new RunSummary { Total = 1, Failed = failCount }, Test_DateTimeSeedFile);
		int actual = RandomSeedBase;

		// May also succeed if we get lucky, but if the other tests are failing,
		// even if this succeeds, there's a good chance it shouldn't.
		Assert.Equal(expected, actual);
	}

	[TestFact]
	[TLabel($"[m] with persisted datetime seed file not changing when (mock) tests fails still")]
	public async Task T019_SaveLocalRandomState() {
		// `async` ensures our `AsyncLocal`s will be under a different context

		FreezeLocalDefaultDateTimeSeed();

		LoadLocalRandomState("", Test_DateTimeSeedFile);
		SaveLocalRandomState(new RunSummary { Total = 1, Failed = 1 }, Test_DateTimeSeedFile);
		string expected = await ReadFirstLineAsync(Test_DateTimeSeedFile);

		// Assure change if seed file will not get loaded
		AdvanceLocalDefaultDateTimeSeed();

		LoadLocalRandomState("", Test_DateTimeSeedFile);
		SaveLocalRandomState(new RunSummary { Total = 1, Failed = 1 }, Test_DateTimeSeedFile);
		string actual = await ReadFirstLineAsync(Test_DateTimeSeedFile);

		Assert.Equal(expected, actual);
	}

	#endregion

#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
}
