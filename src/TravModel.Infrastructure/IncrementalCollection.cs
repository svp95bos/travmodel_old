using Microsoft.EntityFrameworkCore;
using TravModel.Application;
using TravModel.Domain;

namespace TravModel.Infrastructure;

public sealed record ProviderRunFailure(string Provider, string Capability, string Error);

public sealed record IncrementalCollectionResult(
    int RecentStartsWritten,
    int ProfileFactsWritten,
    int EquipmentFactsWritten,
    IReadOnlyList<ProviderRunFailure> Failures);

public sealed class IncrementalCollectionService(TravDbContext dbContext)
{
    public async Task<IncrementalCollectionResult> EnrichEntrantsAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        IReadOnlyCollection<IRecentFormProvider> recentFormProviders,
        IReadOnlyCollection<IEntityProfileProvider> profileProviders,
        IReadOnlyCollection<IEquipmentProvider> equipmentProviders,
        DateTimeOffset asOfUtc,
        CancellationToken cancellationToken)
    {
        var races = await dbContext.Races.AsNoTracking()
            .Include(x => x.Starters)
            .Where(x => x.ScheduledStartUtc >= fromUtc && x.ScheduledStartUtc < toUtc)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var entityIds = races.SelectMany(x => x.Starters)
            .SelectMany(x => new[] { (Type: "Horse", Id: x.HorseId), (Type: "Person", Id: x.DriverId), (Type: "Person", Id: x.TrainerId) })
            .Distinct().ToArray();
        var canonicalIds = entityIds.Select(x => x.Id).ToArray();
        var identities = await dbContext.ExternalIdentities.AsNoTracking()
            .Where(x => canonicalIds.Contains(x.EntityId))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var failures = new List<ProviderRunFailure>();
        var health = new ProviderHealthService(dbContext);
        var ingestion = new EnrichmentIngestionService(dbContext);
        var recentWritten = 0;
        var profileWritten = 0;
        var equipmentWritten = 0;

        foreach (var provider in recentFormProviders)
        {
            if (!await IsApprovedAsync(provider.Name, "recent-form", cancellationToken).ConfigureAwait(false)) continue;
            var references = ReferencesFor(provider.Name, "Horse", identities);
            if (references.Length == 0) continue;
            try
            {
                var values = await provider.GetRecentStartsAsync(references, asOfUtc, cancellationToken).ConfigureAwait(false);
                recentWritten += await ingestion.ImportRecentStartsAsync(values, cancellationToken).ConfigureAwait(false);
                await health.RecordSuccessAsync(provider.Name, "recent-form", asOfUtc, null, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                failures.Add(new ProviderRunFailure(provider.Name, "recent-form", exception.Message));
                await health.RecordFailureAsync(provider.Name, "recent-form", asOfUtc, exception, cancellationToken).ConfigureAwait(false);
                dbContext.ChangeTracker.Clear();
            }
        }

        foreach (var provider in profileProviders)
        {
            if (!await IsApprovedAsync(provider.Name, "profiles", cancellationToken).ConfigureAwait(false)) continue;
            var references = identities.Where(x => x.SourceName == provider.Name)
                .Join(entityIds, x => (x.EntityType, x.EntityId), x => (x.Type, x.Id),
                    (identity, _) => new ProviderEntityReference(identity.EntityId, identity.EntityType, identity.SourceName, identity.ExternalId))
                .Distinct().ToArray();
            if (references.Length == 0) continue;
            try
            {
                var values = await provider.GetProfileFactsAsync(references, asOfUtc, cancellationToken).ConfigureAwait(false);
                profileWritten += await ingestion.ImportFactsAsync(values, cancellationToken).ConfigureAwait(false);
                await health.RecordSuccessAsync(provider.Name, "profiles", asOfUtc, null, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                failures.Add(new ProviderRunFailure(provider.Name, "profiles", exception.Message));
                await health.RecordFailureAsync(provider.Name, "profiles", asOfUtc, exception, cancellationToken).ConfigureAwait(false);
                dbContext.ChangeTracker.Clear();
            }
        }

        foreach (var provider in equipmentProviders)
        {
            if (!await IsApprovedAsync(provider.Name, "equipment", cancellationToken).ConfigureAwait(false)) continue;
            try
            {
                var values = await provider.GetEquipmentFactsAsync(races, asOfUtc, cancellationToken).ConfigureAwait(false);
                equipmentWritten += await ingestion.ImportFactsAsync(values, cancellationToken).ConfigureAwait(false);
                await health.RecordSuccessAsync(provider.Name, "equipment", asOfUtc, null, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                failures.Add(new ProviderRunFailure(provider.Name, "equipment", exception.Message));
                await health.RecordFailureAsync(provider.Name, "equipment", asOfUtc, exception, cancellationToken).ConfigureAwait(false);
                dbContext.ChangeTracker.Clear();
            }
        }

        return new IncrementalCollectionResult(recentWritten, profileWritten, equipmentWritten, failures);
    }

    private static ProviderEntityReference[] ReferencesFor(
        string provider,
        string entityType,
        IEnumerable<ExternalIdentity> identities) => identities
        .Where(x => x.SourceName == provider && x.EntityType == entityType)
        .Select(x => new ProviderEntityReference(x.EntityId, x.EntityType, x.SourceName, x.ExternalId))
        .Distinct().ToArray();

    private Task<bool> IsApprovedAsync(string provider, string capability, CancellationToken cancellationToken) =>
        dbContext.SourceQualifications.AsNoTracking().AnyAsync(x => x.SourceName == provider && x.Capability == capability &&
            x.Status == SourceQualificationStatus.Approved, cancellationToken);
}

public sealed class DataCoverageService(TravDbContext dbContext)
{
    public async Task<DataCoverageReport> BuildAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        DateTimeOffset generatedAtUtc,
        CancellationToken cancellationToken)
    {
        var races = await dbContext.Races.AsNoTracking()
            .Include(x => x.Starters)
            .Where(x => x.ScheduledStartUtc >= fromUtc && x.ScheduledStartUtc < toUtc)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var starters = races.SelectMany(x => x.Starters.Select(s => (Race: x, Starter: s))).ToArray();
        var horseIds = starters.Select(x => x.Starter.HorseId).Distinct().ToArray();
        var earliest = starters.Length == 0 ? fromUtc : starters.Min(x => x.Race.ScheduledStartUtc).AddDays(-90);
        var history = await dbContext.HistoricalStarts.AsNoTracking()
            .Where(x => horseIds.Contains(x.HorseId) && x.StartTimeUtc >= earliest && x.RetrievedAtUtc <= generatedAtUtc)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var historyCounts = history.GroupBy(x => x.HorseId).ToDictionary(x => x.Key, x => x.Count());
        var starterIds = starters.Select(x => x.Starter.Id.ToString()).ToArray();
        var equipmentFields = new[] { "ShoesFront", "ShoesHind", "Shoes", "Sulky", "SulkyType" };
        var observations = await dbContext.Observations.AsNoTracking()
            .Where(x => x.RetrievedAtUtc >= fromUtc.AddDays(-1) && x.RetrievedAtUtc <= generatedAtUtc)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var equipmentStarterIds = observations.Where(x => starterIds.Contains(x.EntityId) && equipmentFields.Contains(x.Field))
            .Select(x => x.EntityId).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        var alerts = await dbContext.ProviderCheckpoints.AsNoTracking()
            .Where(x => x.ConsecutiveFailures > 0)
            .Select(x => $"{x.Provider}/{x.Scope}: {x.ConsecutiveFailures} consecutive failure(s): {x.LastError}")
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var observationsBySource = observations.GroupBy(x => x.SourceName)
            .ToDictionary(x => x.Key, x => x.Count(), StringComparer.OrdinalIgnoreCase);
        var pendingReviews = await dbContext.IdentityReviews.CountAsync(x => x.Status == IdentityReviewStatus.Pending, cancellationToken).ConfigureAwait(false);
        var failedRuns = await dbContext.IngestionRuns.CountAsync(x => x.StartedAtUtc >= fromUtc && x.Status == "Failed", cancellationToken).ConfigureAwait(false);

        return new DataCoverageReport(generatedAtUtc, fromUtc, toUtc, races.Count, starters.Length,
            starters.Count(x => historyCounts.GetValueOrDefault(x.Starter.HorseId) >= 3),
            starters.Count(x => historyCounts.GetValueOrDefault(x.Starter.HorseId) >= 5),
            equipmentStarterIds, pendingReviews, failedRuns, observationsBySource, alerts);
    }
}
