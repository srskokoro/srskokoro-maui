namespace Kokoro.Test.Util.SelfTest;

using Kokoro.Test.Framework;
using System.Globalization;
using Xunit.Sdk;
using static Kokoro.Test.Util.ILocalRandomProvider;

public abstract class ILocalRandomProvider_Test_Base {
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

public sealed class ILocalRandomProvider_Test
	: ILocalRandomProvider_Test_Base, ILocalRandomProvider {

	[Fact]
	public void TestFramework_ProperlySetsUp_RandomSeedBase() {
		Exception? e = Record.Exception(() => {
			_ = RandomSeedBase;
		});
		Assert.Null(e);
	}

	[Fact]
	public void TestFrameworkConfig_DateTimeSeed_Is_EitherValidOrEmpty() {
		Assert.True(
			TryParseDateTimeSeed(TestFrameworkConfig.DateTimeSeed, out _)
			|| string.IsNullOrWhiteSpace(TestFrameworkConfig.DateTimeSeed)
			, "Invalid configuration! Should be either empty or set to a valid ISO 8601 date time string."
		);
	}

	[Theory]
	[InlineData(Test_DateTimeSeed_1, Test_DateTimeSeed_1)]
	[InlineData(Test_DateTimeSeed_2, Test_DateTimeSeed_2)]
	[InlineData(Test_DateTimeSeed_3, Test_DateTimeSeed_3_Alt)]
	public void TryParseDateTimeSeed_Supports_IsoDateTime(string isoDateTimeStrInput, string isoDateTimeStrOutput) {
		Assert.True(
			TryParseDateTimeSeed(isoDateTimeStrInput, out var dto),
			$"Unexpected! ISO 8601 string not supported: {isoDateTimeStrInput}"
		);
		Assert.Equal(
			isoDateTimeStrOutput,
			dto.ToString(DateTimeSeed_ExpectedFormat, CultureInfo.InvariantCulture)
		);
	}

	[Fact]
	public async Task RandomSeedBase_DoesNotChange_OnItsOwn() {
		// `async` ensures our `AsyncLocal`s will be under a different context
		await Task.CompletedTask; // Suppress CS1998

		FreezeLocalDefaultDateTimeSeed();
		int seed1 = RandomSeedBase;

		AdvanceLocalDefaultDateTimeSeed();
		int seed2 = RandomSeedBase;

		Assert.Equal(seed1, seed2);
	}
}

public sealed class ILocalRandomProvider_Test_WithDirSetup
	: ILocalRandomProvider_Test_Base, ILocalRandomProvider
	, IClassFixture<ILocalRandomProvider_Test_WithDirSetup.DirectoryFixture>
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

	public ILocalRandomProvider_Test_WithDirSetup(DirectoryFixture _) {
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

	[Theory]
	[InlineData(Test_DateTimeSeed_1)]
	[InlineData(Test_DateTimeSeed_2)]
	[InlineData(Test_DateTimeSeed_3)]
	public async Task LoadLocalRandomState_DoesNotCreate_DateTimeSeedFile_When_DateTimeSeedOverride_IsValid(string validDateTimeSeed) {
		// `async` ensures our `AsyncLocal`s will be under a different context

		LoadLocalRandomState(validDateTimeSeed, Test_DateTimeSeedFile);
		Assert.False(File.Exists(Test_DateTimeSeedFile), "Should've been not created.");
	}

	[Theory]
	[InlineData("")]
	[InlineData("An invalid content.")]
	public async Task LoadLocalRandomState_Creates_EmptyDateTimeSeedFile_When_DateTimeSeedOverride_IsInvalid(string invalidDateTimeSeed) {
		// `async` ensures our `AsyncLocal`s will be under a different context

		// Also implies that the following will not throw on invalid input
		LoadLocalRandomState(invalidDateTimeSeed, Test_DateTimeSeedFile);

		Assert.True(File.Exists(Test_DateTimeSeedFile), "Should've been created.");
		string contents = await File.ReadAllTextAsync(Test_DateTimeSeedFile);

		Assert.Empty(contents);
	}

	[Theory]
	[InlineData("")]
	[InlineData("An invalid content.")]
	public async Task LoadLocalRandomState_SetsUp_RandomSeedBase_EvenWhen_DateTimeSeedOverride_IsInvalid(string invalidDateTimeSeed) {
		// `async` ensures our `AsyncLocal`s will be under a different context

		// Also implies that this will not throw on invalid input
		LoadLocalRandomState(invalidDateTimeSeed, Test_DateTimeSeedFile);

		Exception? e = Record.Exception(() => {
			_ = RandomSeedBase;
		});

		Assert.Null(e);
	}

	[Theory]
	[InlineData("")]
	[InlineData("An invalid content.")]
	[InlineData("Foo\r\nBar\r\n")]
	[InlineData("Foo\nBar\nBaz")]
	public async Task LoadLocalRandomState_SetsUp_RandomSeedBase_EvenWhen_DateTimeSeedFile_IsInvalid(string invalidDateTimeSeed) {
		// `async` ensures our `AsyncLocal`s will be under a different context

		await File.WriteAllTextAsync(Test_DateTimeSeedFile, invalidDateTimeSeed);

		// Also implies that this will not throw on invalid input
		LoadLocalRandomState("", Test_DateTimeSeedFile);

		Exception? e = Record.Exception(() => {
			_ = RandomSeedBase;
		});

		Assert.Null(e);
	}

	[Theory, CombinatorialData]
	public async Task LoadLocalRandomState_Loads_SameRandomSeedBase_WhetherFromStringOrFile_EvenWhen_FileHasExtraLines(
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

	[Theory]
	[InlineData(Test_DateTimeSeed_1, Test_DateTimeSeed_2)]
	[InlineData(Test_DateTimeSeed_1, Test_DateTimeSeed_3)]
	[InlineData(Test_DateTimeSeed_2, Test_DateTimeSeed_1)]
	[InlineData(Test_DateTimeSeed_2, Test_DateTimeSeed_3)]
	[InlineData(Test_DateTimeSeed_3, Test_DateTimeSeed_1)]
	[InlineData(Test_DateTimeSeed_3, Test_DateTimeSeed_2)]
	public async Task LoadLocalRandomState_Loads_ExpectedRandomSeedBase_While_Ignoring_ExistingDateTimeSeedFile(string dateTimeSeedOverride, string dateTimeSeedFileContent) {
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

	[Theory, CombinatorialData]
	public async Task LoadLocalRandomState_DoesNotModify_ExistingDateTimeSeedFile(
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

	[Fact]
	public async Task LoadLocalRandomState_Twice_Sets_DifferentSeeds() {
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

	[Fact]
	public async Task LoadLocalRandomState_Twice_OnSameDateTimeSeedOverride_Sets_SameSeed() {
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

	/// <summary>
	/// Algorithm tamper protection.
	/// </summary>
	[Theory]
	[InlineData(Test_DateTimeSeed_1, -1870755152)]
	[InlineData(Test_DateTimeSeed_2, -556981469)]
	[InlineData(Test_DateTimeSeed_3, 2103003800)]
	public async Task LoadLocalRandomState_AlgorithmOutput_Is_Still_AsExpected(string dateTimeSeed, int expected) {
		// `async` ensures our `AsyncLocal`s will be under a different context

		LoadLocalRandomState(dateTimeSeed, "");
		int actual = RandomSeedBase;

		Assert.Equal(expected, actual);
	}

	#endregion

	#region `SaveLocalRandomState` tests

	[Fact]
	public async Task SaveLocalRandomState_Deletes_DateTimeSeedFile_When_AllTestsPassed() {
		// `async` ensures our `AsyncLocal`s will be under a different context

		LoadLocalRandomState("", Test_DateTimeSeedFile); // Should at least create an empty file
		Assert.True(File.Exists(Test_DateTimeSeedFile), "Should exist first.");

		SaveLocalRandomState(new RunSummary { Total = 1, Failed = 0 }, Test_DateTimeSeedFile);
		Assert.False(File.Exists(Test_DateTimeSeedFile), "Shouldn't exist anymore.");
	}

	[Theory]
	[InlineData(null)]
	[InlineData($"{Test_DateTimeSeed_1}\r\nAn invalid content.")]
	[InlineData($"{Test_DateTimeSeed_2}\r\nAn invalid content.")]
	[InlineData($"An invalid content.\r\n{Test_DateTimeSeed_3}")]
	[InlineData($"An invalid content.")]
	public async Task SaveLocalRandomState_Persists_ValidDateTimeSeedFile_When_SomeTestsFailed_But_Overwrites_PriorFileContent_IfAny(string? priorFileContent) {
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

	[Theory]
	[InlineData(Test_DateTimeSeed_1)]
	[InlineData(Test_DateTimeSeed_2)]
	[InlineData(Test_DateTimeSeed_3)]
	public async Task SaveLocalRandomState_Saves_ExpectedDateTimeSeed_When_SomeTestsFailed(string dateTimeSeed) {
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

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	public async Task SaveLocalRandomState_DoesNotModify_RandomSeedBase(int failCount) {
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

	[Fact]
	public async Task SaveLocalRandomState_With_PersistedDateTimeSeedFile_NotChanging_When_TestsFailsStill() {
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
