using TravModel.Application;
using TravModel.Domain;

namespace TravModel.Tests;

public sealed class DataQualityTests
{
    [Fact]
    public void SnapshotPlanner_SelectsEachConfiguredWindowOnce()
    {
        var now = new DateTimeOffset(2026, 9, 12, 12, 0, 0, TimeSpan.Zero);
        var dueRace = TestData.Race(now.AddMinutes(17));
        var tooLate = TestData.Race(now.AddMinutes(14));

        var due = SnapshotPlanner.GetDueSnapshots([dueRace, tooLate], now, TimeSpan.FromMinutes(5));

        var snapshot = Assert.Single(due);
        Assert.Equal(dueRace.Id, snapshot.RaceId);
        Assert.Equal(15, snapshot.MinutesBeforeStart);
    }

    [Fact]
    public void ValidatorRejectsDuplicateRaceNumbersAndMissingStarters()
    {
        var date = new DateOnly(2026, 9, 12);
        var start = new DateTimeOffset(2026, 9, 12, 12, 0, 0, TimeSpan.Zero);
        var meeting = new ProviderMeeting("m1", "t1", "Track", date,
        [
            new ProviderRace("r1", 1, start, 2140, StartMethod.Auto, "Harness", "", []),
            new ProviderRace("r2", 1, start.AddHours(1), 2140, StartMethod.Auto, "Harness", "", [])
        ]);
        var envelope = new SourceEnvelope<ProviderMeeting>(meeting, "test", new Uri("https://example.test"), start, start, "{}");

        var issues = ProviderDataValidator.ValidateMeetings([envelope], date, date);

        Assert.Contains(issues, x => x.Code == "missing-starters" && x.IsFatal);
        Assert.Contains(issues, x => x.Code == "race-number-mismatch" && x.IsFatal);
    }
}
