using Microsoft.EntityFrameworkCore;
using TravModel.Application;
using TravModel.Domain;
using TravModel.Infrastructure;

namespace TravModel.Tests;

public sealed class AccumulatingHistoryTests
{
    [Fact]
    public async Task OfficialIngestion_CreatesIdentityCrosswalkAndReusableHistory()
    {
        await using var db = CreateContext();
        var start = Time("2026-09-12T18:00:00Z");
        var meeting = new ProviderMeeting("m1", "track1", "Solvalla", new DateOnly(2026, 9, 12),
        [
            new ProviderRace("r1", 1, start, 2140, StartMethod.Auto, "Harness", "", [
                new ProviderStarter("h1", "Horse", "d1", "Driver", "t1", "Trainer", 1, 1, 2140, false)])
        ]);
        var meetingEnvelope = new SourceEnvelope<ProviderMeeting>(meeting, "SvenskTravsport", new Uri("https://example.test/meeting"),
            start.AddDays(-1), start.AddDays(-1), "{}");
        var service = new RaceIngestionService(db);

        await service.ImportMeetingsAsync([meetingEnvelope], CancellationToken.None);
        var result = new ProviderResult("r1", "h1", 1, "1", 72.1m, null, 50_000m, false, "Won");
        await service.ImportResultsAsync([
            new SourceEnvelope<ProviderResult>(result, "SvenskTravsport", new Uri("https://example.test/result"),
                start.AddHours(2), start.AddHours(1), "{}")], CancellationToken.None);

        Assert.Equal(7, await db.ExternalIdentities.CountAsync());
        Assert.Equal(9, await db.Observations.CountAsync(x => x.IngestionRunId != null && x.RetrievedAtUtc < start));
        Assert.Single(await db.HistoricalStarts.ToListAsync());
        Assert.Single(await db.HistoricalStartRevisions.ToListAsync());
    }

    [Fact]
    public async Task RecentHistoryImport_IsIdempotentAcrossOverlappingWindows()
    {
        await using var db = CreateContext();
        var horseId = Guid.NewGuid();
        db.ExternalIdentities.Add(new ExternalIdentity
        {
            EntityType = "Horse",
            EntityId = horseId,
            SourceName = "ATG",
            ExternalId = "h1",
            FirstSeenAtUtc = Time("2026-09-01T00:00:00Z"),
            LastSeenAtUtc = Time("2026-09-01T00:00:00Z"),
            IsVerified = true
        });
        await db.SaveChangesAsync();
        var value = new ProviderRecentStart("h1", "r1", Time("2026-08-01T12:00:00Z"), "Solvalla", 3, 2140,
            StartMethod.Auto, 4, 1, 72.4m, 3.2m, "BarefootFront", "American", "Fast", 100_000m, false, "Strong finish");
        var envelope = new SourceEnvelope<ProviderRecentStart>(value, "ATG", new Uri("https://example.test/r1"),
            Time("2026-09-01T10:00:00Z"), Time("2026-08-01T13:00:00Z"), "{}");
        var service = new EnrichmentIngestionService(db);

        var first = await service.ImportRecentStartsAsync([envelope], CancellationToken.None);
        var second = await service.ImportRecentStartsAsync([envelope], CancellationToken.None);

        Assert.True(first > 0);
        Assert.Equal(0, second);
        Assert.Single(await db.HistoricalStarts.ToListAsync());
        Assert.Single(await db.HistoricalStartRevisions.ToListAsync());
        Assert.Equal(15, await db.Observations.CountAsync());
    }

    [Fact]
    public async Task RecentHistoryImport_RejectsFutureOrMalformedStarts()
    {
        await using var db = CreateContext();
        var retrieved = Time("2026-09-01T10:00:00Z");
        var value = new ProviderRecentStart("h1", "r1", retrieved.AddMinutes(1), "Solvalla", 1, 2140,
            StartMethod.Auto, 1, null, null, null, null, null, null, null, false, null);
        var envelope = new SourceEnvelope<ProviderRecentStart>(value, "ATG", new Uri("https://example.test"), retrieved, null, "{}");

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            new EnrichmentIngestionService(db).ImportRecentStartsAsync([envelope], CancellationToken.None));
    }

    [Fact]
    public async Task Repository_UsesRevisionKnownAtCutoff_NotLaterCorrection()
    {
        await using var db = CreateContext();
        var targetStart = Time("2026-09-12T18:00:00Z");
        var race = TestData.Race(targetStart);
        var starter = TestData.Starter(race, 1);
        race.Starters.Add(starter);
        db.Races.Add(race);
        var history = TestData.HistoricalStart(starter.HorseId, targetStart.AddDays(-10), 2);
        history.RetrievedAtUtc = targetStart.AddDays(-9);
        db.HistoricalStarts.Add(history);
        db.HistoricalStartRevisions.Add(Revision(history, 1, targetStart.AddDays(-8)));
        db.HistoricalStartRevisions.Add(Revision(history, 2, targetStart.AddMinutes(-5)));
        await db.SaveChangesAsync();

        var input = await new SqlTravRepository(db).GetFeatureInputAsync(race.Id, targetStart.AddMinutes(-15), CancellationToken.None);
        var vector = PointInTimeFeatureBuilder.Build(input!, targetStart.AddMinutes(-15)).Starters.Single();

        Assert.Equal(1f, vector.RecentWinRate);
    }

    [Fact]
    public void ShadowFeatures_CompareEquipmentWithoutChangingProductionVector()
    {
        var start = Time("2026-09-12T18:00:00Z");
        var race = TestData.Race(start);
        var starter = TestData.Starter(race, 1);
        race.Starters.Add(starter);
        var history = TestData.HistoricalStart(starter.HorseId, start.AddDays(-10), 2);
        history.Shoes = "Shod";
        history.Sulky = "Regular";
        var shoes = ObservationFactory.Create("Starter", starter.Id.ToString("D"), "Shoes", "ATG", "https://example.test",
            start.AddMinutes(-20), start.AddMinutes(-20), "Barefoot", "Barefoot", true);
        var sulky = ObservationFactory.Create("Starter", starter.Id.ToString("D"), "SulkyType", "ATG", "https://example.test",
            start.AddMinutes(-20), start.AddMinutes(-20), "American", "American", true);
        var input = new RaceFeatureInput(race,
            [new StarterFeatureHistory(starter, [history], new RollingRate(0, 0, 0), new RollingRate(0, 0, 0))], [shoes, sulky]);

        var shadow = PointInTimeShadowFeatureBuilder.Build(input, start.AddMinutes(-15)).Starters.Single();

        Assert.True(shadow.ShoesChanged);
        Assert.True(shadow.SulkyChanged);
        Assert.Equal("Barefoot", shadow.ShoesToday);
    }

    [Fact]
    public async Task Collection_DegradesAndRecordsProviderFailure()
    {
        await using var db = CreateContext();
        var start = Time("2026-09-13T12:00:00Z");
        var race = TestData.Race(start);
        var starter = TestData.Starter(race, 1);
        race.Starters.Add(starter);
        db.Races.Add(race);
        db.ExternalIdentities.Add(new ExternalIdentity
        {
            EntityType = "Horse",
            EntityId = starter.HorseId,
            SourceName = "Broken",
            ExternalId = "h1",
            FirstSeenAtUtc = start.AddDays(-1),
            LastSeenAtUtc = start.AddDays(-1),
            IsVerified = true
        });
        db.SourceQualifications.Add(new SourceQualification
        {
            SourceName = "Broken",
            Capability = "recent-form",
            BaseUri = new Uri("https://example.test"),
            Status = SourceQualificationStatus.Approved,
            CheckedAtUtc = start.AddDays(-1)
        });
        await db.SaveChangesAsync();

        var result = await new IncrementalCollectionService(db).EnrichEntrantsAsync(start.AddDays(-1), start.AddDays(1),
            [new BrokenRecentFormProvider()], [], [], start.AddHours(-1), CancellationToken.None);

        Assert.Single(result.Failures);
        var checkpoint = Assert.Single(await db.ProviderCheckpoints.ToListAsync());
        Assert.Equal(1, checkpoint.ConsecutiveFailures);
        Assert.Single(await db.IngestionRuns.Where(x => x.Status == "Failed").ToListAsync());
    }

    private static TravDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TravDbContext>()
            .UseInMemoryDatabase($"travmodel-{Guid.NewGuid():N}")
            .Options;
        return new TravDbContext(options);
    }

    private static HistoricalStartRevision Revision(HistoricalStart history, int finish, DateTimeOffset retrieved) => new()
    {
        HistoricalStart = history,
        HistoricalStartId = history.Id,
        HorseId = history.HorseId,
        StartTimeUtc = history.StartTimeUtc,
        TrackName = history.TrackName,
        RaceNumber = history.RaceNumber,
        DistanceMetres = history.DistanceMetres,
        StartMethod = history.StartMethod,
        PostPosition = history.PostPosition,
        FinishPosition = finish,
        Galloped = false,
        SourceName = "SvenskTravsport",
        SourceUrl = "https://example.test/result",
        RetrievedAtUtc = retrieved,
        ObservedAtUtc = retrieved,
        ContentHash = Guid.NewGuid().ToString("N")
    };

    private static DateTimeOffset Time(string value) => DateTimeOffset.Parse(value, System.Globalization.CultureInfo.InvariantCulture);

    private sealed class BrokenRecentFormProvider : IRecentFormProvider
    {
        public string Name => "Broken";
        public Task<IReadOnlyList<SourceEnvelope<ProviderRecentStart>>> GetRecentStartsAsync(
            IReadOnlyCollection<ProviderEntityReference> horses,
            DateTimeOffset asOfUtc,
            CancellationToken cancellationToken) => throw new HttpRequestException("Source unavailable.");
    }
}
