using Microsoft.EntityFrameworkCore;
using TravModel.Application;
using TravModel.Domain;

namespace TravModel.Infrastructure;

public sealed class SqlTravRepository(TravDbContext dbContext) : ITravRepository
{
    public async Task<IReadOnlyList<Race>> GetRacesAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken) =>
        await dbContext.Races
            .AsNoTracking()
            .Include(x => x.Meeting).ThenInclude(x => x!.Track)
            .Include(x => x.Starters).ThenInclude(x => x.Horse)
            .Where(x => x.ScheduledStartUtc >= fromUtc && x.ScheduledStartUtc < toUtc)
            .OrderBy(x => x.ScheduledStartUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<RaceFeatureInput?> GetFeatureInputAsync(
        Guid raceId,
        DateTimeOffset cutoffUtc,
        CancellationToken cancellationToken)
    {
        var race = await dbContext.Races
            .AsNoTracking()
            .Include(x => x.Starters)
            .SingleOrDefaultAsync(x => x.Id == raceId, cancellationToken)
            .ConfigureAwait(false);
        if (race is null) return null;

        var horseIds = race.Starters.Select(x => x.HorseId).ToArray();
        var driverIds = race.Starters.Select(x => x.DriverId).ToArray();
        var trainerIds = race.Starters.Select(x => x.TrainerId).ToArray();
        var historyStart = cutoffUtc.AddDays(-90);
        var historyRows = await dbContext.HistoricalStarts
            .AsNoTracking()
            .Where(x => horseIds.Contains(x.HorseId) && x.StartTimeUtc >= historyStart && x.StartTimeUtc < cutoffUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var history = await EffectiveHistoryAsync(historyRows, cutoffUtc, cancellationToken).ConfigureAwait(false);
        var observationEntityIds = race.Starters.Select(x => x.Id.ToString()).Append(raceId.ToString()).ToArray();
        var observations = await dbContext.Observations
            .AsNoTracking()
            .Where(x => x.RetrievedAtUtc <= cutoffUtc && observationEntityIds.Contains(x.EntityId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var rateStart = cutoffUtc.AddDays(-30);
        var combinationRows = await dbContext.HistoricalStarts
            .AsNoTracking()
            .Where(x => x.StartTimeUtc >= rateStart && x.StartTimeUtc < cutoffUtc &&
                        ((x.DriverId != null && driverIds.Contains(x.DriverId.Value)) ||
                         (x.TrainerId != null && trainerIds.Contains(x.TrainerId.Value))))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var combinationHistory = await EffectiveHistoryAsync(combinationRows, cutoffUtc, cancellationToken).ConfigureAwait(false);

        var starterFeatures = race.Starters.Select(starter => new StarterFeatureHistory(
            starter,
            history.Where(x => x.HorseId == starter.HorseId).ToArray(),
            ToRate(combinationHistory.Where(x => x.DriverId == starter.DriverId)),
            ToRate(combinationHistory.Where(x => x.TrainerId == starter.TrainerId)))).ToArray();
        return new RaceFeatureInput(race, starterFeatures, observations);
    }

    public async Task AppendObservationsAsync(
        IReadOnlyCollection<Observation> observations,
        CancellationToken cancellationToken)
    {
        if (observations.Count == 0) return;
        var hashes = observations.Select(x => x.ContentHash).Distinct(StringComparer.Ordinal).ToArray();
        var existing = await dbContext.Observations
            .Where(x => hashes.Contains(x.ContentHash))
            .Select(x => x.ContentHash)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var existingSet = existing.ToHashSet(StringComparer.Ordinal);
        await dbContext.Observations
            .AddRangeAsync(observations.Where(x => existingSet.Add(x.ContentHash)), cancellationToken)
            .ConfigureAwait(false);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task SavePredictionRunAsync(PredictionRun run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        var raceStart = await dbContext.Races
            .Where(x => x.Id == run.RaceId)
            .Select(x => x.ScheduledStartUtc)
            .SingleAsync(cancellationToken)
            .ConfigureAwait(false);
        if (run.FeatureCutoffUtc >= raceStart || run.CreatedAtUtc >= raceStart)
        {
            throw new InvalidOperationException("A pre-race prediction must be created before the race starts.");
        }

        dbContext.PredictionRuns.Add(run);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveBettingMarketMembershipsAsync(
        IReadOnlyCollection<BettingMarketMembership> memberships,
        CancellationToken cancellationToken)
    {
        foreach (var membership in memberships)
        {
            var exists = await dbContext.BettingMarketMemberships.AnyAsync(
                x => x.RaceId == membership.RaceId && x.Product == membership.Product && x.ProductId == membership.ProductId,
                cancellationToken).ConfigureAwait(false);
            if (!exists) dbContext.BettingMarketMemberships.Add(membership);
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static RollingRate ToRate(IEnumerable<HistoricalStart> starts)
    {
        var values = starts.Where(x => x.FinishPosition.HasValue).ToArray();
        return new RollingRate(values.Length, values.Count(x => x.FinishPosition == 1), values.Count(x => x.FinishPosition <= 3));
    }

    private async Task<IReadOnlyList<HistoricalStart>> EffectiveHistoryAsync(
        List<HistoricalStart> rows,
        DateTimeOffset cutoffUtc,
        CancellationToken cancellationToken)
    {
        if (rows.Count == 0) return [];
        var ids = rows.Select(x => x.Id).ToArray();
        var revisions = await dbContext.HistoricalStartRevisions.AsNoTracking()
            .Where(x => ids.Contains(x.HistoricalStartId) && x.RetrievedAtUtc <= cutoffUtc &&
                        (x.ObservedAtUtc == null || x.ObservedAtUtc <= cutoffUtc))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var byStart = revisions.GroupBy(x => x.HistoricalStartId).ToDictionary(x => x.Key, x => x
            .OrderByDescending(value => SourcePriority(value.SourceName))
            .ThenByDescending(value => value.RetrievedAtUtc)
            .ToArray());
        var output = new List<HistoricalStart>();
        foreach (var row in rows)
        {
            if (byStart.TryGetValue(row.Id, out var startRevisions))
            {
                output.Add(Project(row, startRevisions));
            }
            else if (row.RetrievedAtUtc <= cutoffUtc && (row.ObservedAtUtc is null || row.ObservedAtUtc <= cutoffUtc))
            {
                output.Add(row);
            }
        }
        return output;
    }

    private static HistoricalStart Project(HistoricalStart row, HistoricalStartRevision[] revisions)
    {
        var primary = revisions[0];
        static T? First<T>(IEnumerable<T?> values) where T : class => values.FirstOrDefault(x => x is not null);
        static T? FirstValue<T>(IEnumerable<T?> values) where T : struct => values.FirstOrDefault(x => x.HasValue);
        return new HistoricalStart
        {
            Id = row.Id,
            HorseId = primary.HorseId,
            DriverId = FirstValue(revisions.Select(x => x.DriverId)),
            TrainerId = FirstValue(revisions.Select(x => x.TrainerId)),
            ExternalRaceId = row.ExternalRaceId,
            CanonicalStartKey = row.CanonicalStartKey,
            StartTimeUtc = primary.StartTimeUtc,
            TrackName = primary.TrackName,
            RaceNumber = primary.RaceNumber,
            DistanceMetres = primary.DistanceMetres,
            StartMethod = primary.StartMethod,
            PostPosition = primary.PostPosition,
            FinishPosition = FirstValue(revisions.Select(x => x.FinishPosition)),
            KilometerTimeSeconds = FirstValue(revisions.Select(x => x.KilometerTimeSeconds)),
            Odds = FirstValue(revisions.Select(x => x.Odds)),
            Shoes = First(revisions.Select(x => x.Shoes)),
            Sulky = First(revisions.Select(x => x.Sulky)),
            TrackCondition = First(revisions.Select(x => x.TrackCondition)),
            PrizeMoneySek = FirstValue(revisions.Select(x => x.PrizeMoneySek)),
            Galloped = primary.Galloped,
            RaceComment = First(revisions.Select(x => x.RaceComment)),
            SourceName = primary.SourceName,
            SourceUrl = primary.SourceUrl,
            RetrievedAtUtc = revisions.Max(x => x.RetrievedAtUtc),
            ObservedAtUtc = primary.ObservedAtUtc,
            FirstSeenAtUtc = row.FirstSeenAtUtc,
            LastSeenAtUtc = row.LastSeenAtUtc,
            CompletenessFlags = row.CompletenessFlags
        };
    }

    private static int SourcePriority(string source) => source switch
    {
        "SvenskTravsport" => 400,
        "ATG" => 300,
        "Skoinfo" => 200,
        "Travmaskinen" => 100,
        _ => 0
    };
}
