using TravModel.Domain;

namespace TravModel.Tests;

internal static class TestData
{
    public static Race Race(DateTimeOffset start) => new()
    {
        ExternalSource = "test",
        ExternalId = Guid.NewGuid().ToString(),
        RaceNumber = 1,
        ScheduledStartUtc = start,
        DistanceMetres = 2140,
        StartMethod = StartMethod.Auto,
        RaceType = "Harness",
        ConditionsText = "Synthetic test race"
    };

    public static Starter Starter(Race race, int number) => new()
    {
        RaceId = race.Id,
        HorseId = Guid.NewGuid(),
        DriverId = Guid.NewGuid(),
        TrainerId = Guid.NewGuid(),
        HorseNumber = number,
        PostPosition = number,
        DistanceMetres = race.DistanceMetres
    };

    public static HistoricalStart HistoricalStart(Guid horseId, DateTimeOffset start, int finish) => new()
    {
        HorseId = horseId,
        ExternalRaceId = Guid.NewGuid().ToString(),
        CanonicalStartKey = Guid.NewGuid().ToString(),
        StartTimeUtc = start,
        TrackName = "Test",
        RaceNumber = 1,
        DistanceMetres = 2140,
        StartMethod = StartMethod.Auto,
        PostPosition = 1,
        FinishPosition = finish,
        SourceName = "test",
        SourceUrl = "https://example.test/history",
        RetrievedAtUtc = start.AddHours(1),
        FirstSeenAtUtc = start.AddHours(1),
        LastSeenAtUtc = start.AddHours(1),
        CompletenessFlags = string.Empty
    };
}
