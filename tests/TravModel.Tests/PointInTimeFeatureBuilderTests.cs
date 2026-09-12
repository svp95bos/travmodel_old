using TravModel.Application;
using TravModel.Domain;
using TravModel.Infrastructure;

namespace TravModel.Tests;

public sealed class PointInTimeFeatureBuilderTests
{
    [Fact]
    public void Build_UsesOnlyInformationAvailableAtCutoff()
    {
        var start = new DateTimeOffset(2026, 9, 12, 18, 0, 0, TimeSpan.Zero);
        var race = TestData.Race(start);
        var starter = TestData.Starter(race, 1);
        race.Starters.Add(starter);
        var before = ObservationFactory.Create("Starter", starter.Id.ToString(), "BettingPercentage", "ATG", "https://example.test/before",
            start.AddMinutes(-16), start.AddMinutes(-16), "18.4", "18.4", true);
        var after = ObservationFactory.Create("Starter", starter.Id.ToString(), "BettingPercentage", "ATG", "https://example.test/after",
            start.AddMinutes(-14), start.AddMinutes(-14), "99", "99", true);
        var history = new[]
        {
            TestData.HistoricalStart(starter.HorseId, start.AddDays(-10), 1),
            TestData.HistoricalStart(starter.HorseId, start.AddDays(1), 1)
        };
        var input = new RaceFeatureInput(race,
            [new StarterFeatureHistory(starter, history, new RollingRate(10, 2, 4), new RollingRate(20, 3, 8))],
            [before, after]);

        var result = PointInTimeFeatureBuilder.Build(input, start.AddMinutes(-15));

        Assert.Equal(18.4f, result.Starters.Single().BettingPercentage);
        Assert.Equal(1, result.Starters.Single().RecentStarts);
    }

    [Fact]
    public void Build_RejectsCutoffAtOrAfterStart()
    {
        var start = DateTimeOffset.UtcNow;
        var race = TestData.Race(start);
        Assert.Throws<ArgumentOutOfRangeException>(() => PointInTimeFeatureBuilder.Build(new RaceFeatureInput(race, [], []), start));
    }

    [Fact]
    public void Build_IsDeterministic()
    {
        var start = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var race = TestData.Race(start);
        var starter = TestData.Starter(race, 1);
        race.Starters.Add(starter);
        var input = new RaceFeatureInput(race, [new StarterFeatureHistory(starter, [], new RollingRate(0, 0, 0), new RollingRate(0, 0, 0))], []);

        Assert.Equal(
            PointInTimeFeatureBuilder.Build(input, start.AddMinutes(-15)).DatasetFingerprint,
            PointInTimeFeatureBuilder.Build(input, start.AddMinutes(-15)).DatasetFingerprint);
    }

    [Fact]
    public void Build_PreservesMissingNumericValues()
    {
        var start = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var race = TestData.Race(start);
        var starter = TestData.Starter(race, 1);
        race.Starters.Add(starter);
        var input = new RaceFeatureInput(race, [new StarterFeatureHistory(starter, [], new RollingRate(0, 0, 0), new RollingRate(0, 0, 0))], []);

        var vector = PointInTimeFeatureBuilder.Build(input, start.AddMinutes(-15)).Starters.Single();

        Assert.True(float.IsNaN(vector.BettingPercentage));
        Assert.True(float.IsNaN(vector.DriverWinRate30Days));
        Assert.True(float.IsNaN(vector.MeanKilometerTimeSeconds));
    }
}
