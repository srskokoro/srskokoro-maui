﻿namespace Kokoro.Test.Util.Framework;
using Blake2Fast;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Globalization;
using Xunit.Sdk;

public interface IRandomizedTestEstablisher {
	private static readonly AsyncLocal<RandomHolder> al_RandomHolder = new();

	private sealed class RandomHolder {
		internal readonly int _RandomSeedBase;
		internal readonly DateTimeOffset _RandomSeed_DateTimeComponent;

		public RandomHolder(DateTimeOffset randomSeed_DateTimeComponent) {
			Span<byte> unixSecondsBytes = stackalloc byte[sizeof(long)];
			BinaryPrimitives.WriteInt64BigEndian(destination: unixSecondsBytes, randomSeed_DateTimeComponent.ToUnixTimeSeconds());

			Span<byte> unixSecondsHashBytes = stackalloc byte[sizeof(int)];
			Blake2b.ComputeAndWriteHash(sizeof(int), input: unixSecondsBytes, output: unixSecondsHashBytes);

			_RandomSeedBase = BinaryPrimitives.ReadInt32BigEndian(unixSecondsHashBytes);
			_RandomSeed_DateTimeComponent = randomSeed_DateTimeComponent;
		}

		internal readonly ConcurrentDictionary<Type, Random> _RandomPerType = new();
	}

	internal interface ILocalRandomAccess {

		protected static Random GetRandom(Type type) {
			var rh = al_RandomHolder.Value!;
			return rh._RandomPerType.GetOrAdd(type
				, static (type, randomSeedBase) => new(unchecked(randomSeedBase + type.ToString().GetDjb2HashCode()))
				, rh._RandomSeedBase);
		}
	}

	// --

	private protected const string DateTimeSeed_ExpectedFormat = "yyyy-MM-ddTHH:mm:ssK";

	private protected static bool TryParseDateTimeSeed([NotNullWhen(true)] string? input, out DateTimeOffset result) {
		return DateTimeOffset.TryParseExact(input, DateTimeSeed_ExpectedFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out result);
	}

	protected static void LoadLocalRandomState(string dateTimeSeedOverride, string dateTimeSeedFile) {
		if (!TryParseDateTimeSeed(dateTimeSeedOverride, out var dtSeed)) {
			using var reader = new StreamReader(dateTimeSeedFile, new FileStreamOptions() {
				Mode = FileMode.OpenOrCreate,
				BufferSize = "0000-12-22T03:33:38+08:00\r\n".Length * 4, // In case UTF-32
			});
			string? dtSeedStr = reader.ReadLine();
			if (dtSeedStr == null || !TryParseDateTimeSeed(dtSeedStr, out dtSeed)) {
				dtSeed = al_UtcNowOverride.Value ?? DateTimeOffset.UtcNow;
			}
		}
		al_RandomHolder.Value = new(dtSeed);
	}

	protected static void SaveLocalRandomState(RunSummary testSummary, string dateTimeSeedFile) {
		if (testSummary.Failed > 0) {
			File.WriteAllText(dateTimeSeedFile, al_RandomHolder.Value!._RandomSeed_DateTimeComponent
				.ToString(DateTimeSeed_ExpectedFormat, CultureInfo.InvariantCulture));
		} else {
			File.Delete(dateTimeSeedFile);
		}
	}

	// --

	/// <exception cref="NullReferenceException">When test framework is not set up properly.</exception>
	private protected static int RandomSeedBase => al_RandomHolder.Value!._RandomSeedBase;

	private static readonly AsyncLocal<DateTimeOffset?> al_UtcNowOverride = new();

	private protected static void FreezeLocalDefaultDateTimeSeed() {
		al_UtcNowOverride.Value = DateTimeOffset.UtcNow;
	}

	private protected static void AdvanceLocalDefaultDateTimeSeed() {
		var utcNowOverride = al_UtcNowOverride.Value ?? throw new InvalidOperationException($"Must first call `{nameof(FreezeLocalDefaultDateTimeSeed)}()`");
		al_UtcNowOverride.Value = utcNowOverride + new TimeSpan(TimeSpan.TicksPerSecond);
	}

	private protected static void RestoreLocalDefaultDateTimeSeed() {
		al_UtcNowOverride.Value = null;
	}
}
