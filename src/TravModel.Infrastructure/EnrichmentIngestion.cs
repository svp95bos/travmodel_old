using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TravModel.Application;
using TravModel.Domain;

namespace TravModel.Infrastructure;

public sealed class ExternalIdentityResolver(TravDbContext dbContext)
{
    public async Task<Guid?> ResolveAsync(
        string entityType,
        string sourceName,
        string externalId,
        DateTimeOffset seenAtUtc,
        CancellationToken cancellationToken)
    {
        var identity = await dbContext.ExternalIdentities.SingleOrDefaultAsync(x =>
            x.EntityType == entityType && x.SourceName == sourceName && x.ExternalId == externalId,
            cancellationToken).ConfigureAwait(false);
        if (identity is null) return null;
        if (seenAtUtc > identity.LastSeenAtUtc) identity.LastSeenAtUtc = seenAtUtc;
        return identity.EntityId;
    }

    public async Task AddReviewAsync(
        string entityType,
        string sourceName,
        string externalId,
        string candidateName,
        string reason,
        DateTimeOffset createdAtUtc,
        CancellationToken cancellationToken)
    {
        var exists = await dbContext.IdentityReviews.AnyAsync(x =>
            x.EntityType == entityType && x.SourceName == sourceName && x.ExternalId == externalId &&
            x.Status == IdentityReviewStatus.Pending, cancellationToken).ConfigureAwait(false);
        if (exists) return;
        dbContext.IdentityReviews.Add(new IdentityReview
        {
            EntityType = entityType,
            SourceName = sourceName,
            ExternalId = externalId,
            CandidateName = candidateName,
            Reason = reason,
            Status = IdentityReviewStatus.Pending,
            CreatedAtUtc = createdAtUtc
        });
    }
}

public sealed class EnrichmentIngestionService(TravDbContext dbContext)
{
    public async Task<int> ImportFactsAsync(
        IReadOnlyCollection<SourceEnvelope<ProviderFact>> envelopes,
        CancellationToken cancellationToken)
    {
        if (envelopes.Count == 0) return 0;
        if (envelopes.Any(x => string.IsNullOrWhiteSpace(x.Value.EntityType) || string.IsNullOrWhiteSpace(x.Value.ExternalId) ||
                               string.IsNullOrWhiteSpace(x.Value.Field)))
            throw new InvalidDataException("Enrichment facts require entity type, external identifier, and field name.");
        var run = BeginRun(envelopes.First().SourceName, envelopes.Count);
        var resolver = new ExternalIdentityResolver(dbContext);
        var observations = new List<Observation>();
        foreach (var envelope in envelopes)
        {
            var fact = envelope.Value;
            var entityId = await resolver.ResolveAsync(fact.EntityType, envelope.SourceName, fact.ExternalId,
                envelope.RetrievedAtUtc, cancellationToken).ConfigureAwait(false);
            if (entityId is null)
            {
                await resolver.AddReviewAsync(fact.EntityType, envelope.SourceName, fact.ExternalId, string.Empty,
                    "No external-identity mapping exists for an enrichment fact.", envelope.RetrievedAtUtc, cancellationToken).ConfigureAwait(false);
                continue;
            }

            var observation = ObservationFactory.Create(fact.EntityType, entityId.Value.ToString("D", CultureInfo.InvariantCulture),
                fact.Field, envelope.SourceName, envelope.SourceUrl.ToString(), envelope.RetrievedAtUtc,
                fact.ObservedAtUtc ?? envelope.ObservedAtUtc, fact.RawValue, fact.NormalizedValue,
                fact.IsAuthoritative, fact.State, fact.ValidAtUtc);
            observation.IngestionRun = run;
            observations.Add(observation);
        }

        var written = await AddNewObservationsAsync(observations, cancellationToken).ConfigureAwait(false);
        CompleteRun(run, written);
        AddArtifactManifests(run, envelopes);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return written;
    }

    public async Task<int> ImportRecentStartsAsync(
        IReadOnlyCollection<SourceEnvelope<ProviderRecentStart>> envelopes,
        CancellationToken cancellationToken)
    {
        if (envelopes.Count == 0) return 0;
        ValidateRecentStarts(envelopes);
        var run = BeginRun(envelopes.First().SourceName, envelopes.Count);
        var resolver = new ExternalIdentityResolver(dbContext);
        var written = 0;
        var observations = new List<Observation>();
        foreach (var envelope in envelopes.OrderBy(x => x.Value.StartTimeUtc))
        {
            var incoming = envelope.Value;
            var horseId = await resolver.ResolveAsync("Horse", envelope.SourceName, incoming.HorseExternalId,
                envelope.RetrievedAtUtc, cancellationToken).ConfigureAwait(false);
            if (horseId is null)
            {
                await resolver.AddReviewAsync("Horse", envelope.SourceName, incoming.HorseExternalId, string.Empty,
                    "Recent history could not be attached without a stable horse identity.", envelope.RetrievedAtUtc, cancellationToken).ConfigureAwait(false);
                continue;
            }
            var driverId = incoming.DriverExternalId is null ? null : await resolver.ResolveAsync("Person", envelope.SourceName,
                incoming.DriverExternalId, envelope.RetrievedAtUtc, cancellationToken).ConfigureAwait(false);
            var trainerId = incoming.TrainerExternalId is null ? null : await resolver.ResolveAsync("Person", envelope.SourceName,
                incoming.TrainerExternalId, envelope.RetrievedAtUtc, cancellationToken).ConfigureAwait(false);

            var raceId = await resolver.ResolveAsync("Race", envelope.SourceName, incoming.ExternalRaceId,
                envelope.RetrievedAtUtc, cancellationToken).ConfigureAwait(false);
            var canonicalKey = raceId?.ToString("D", CultureInfo.InvariantCulture)
                ?? $"event:{incoming.StartTimeUtc.ToUniversalTime():O}:{NormalizeKey(incoming.TrackName)}:{incoming.RaceNumber}";
            var historical = await dbContext.HistoricalStarts.SingleOrDefaultAsync(x =>
                x.HorseId == horseId.Value && x.CanonicalStartKey == canonicalKey, cancellationToken).ConfigureAwait(false);
            if (historical is null)
            {
                historical = new HistoricalStart
                {
                    HorseId = horseId.Value,
                    DriverId = driverId,
                    TrainerId = trainerId,
                    ExternalRaceId = incoming.ExternalRaceId,
                    CanonicalStartKey = canonicalKey,
                    StartTimeUtc = incoming.StartTimeUtc,
                    TrackName = incoming.TrackName,
                    RaceNumber = incoming.RaceNumber,
                    DistanceMetres = incoming.DistanceMetres,
                    StartMethod = incoming.StartMethod,
                    PostPosition = incoming.PostPosition,
                    SourceName = envelope.SourceName,
                    SourceUrl = envelope.SourceUrl.ToString(),
                    RetrievedAtUtc = envelope.RetrievedAtUtc,
                    ObservedAtUtc = envelope.ObservedAtUtc,
                    FirstSeenAtUtc = envelope.RetrievedAtUtc,
                    LastSeenAtUtc = envelope.RetrievedAtUtc,
                    CompletenessFlags = string.Empty
                };
                dbContext.HistoricalStarts.Add(historical);
                written++;
            }

            if (SourcePriority(envelope.SourceName) >= SourcePriority(historical.SourceName))
                ApplyProjection(historical, incoming, envelope);
            historical.DriverId = driverId ?? historical.DriverId;
            historical.TrainerId = trainerId ?? historical.TrainerId;
            historical.LastSeenAtUtc = Max(historical.LastSeenAtUtc, envelope.RetrievedAtUtc);
            historical.CompletenessFlags = Completeness(incoming);
            var revision = HistoricalStartRevisionFactory.Create(historical, incoming, envelope);
            var revisionExists = dbContext.HistoricalStartRevisions.Local.Any(x => x.ContentHash == revision.ContentHash) ||
                await dbContext.HistoricalStartRevisions.AnyAsync(x => x.ContentHash == revision.ContentHash, cancellationToken).ConfigureAwait(false);
            if (!revisionExists)
            {
                dbContext.HistoricalStartRevisions.Add(revision);
                written++;
            }

            foreach (var observation in HistoryObservations(historical, incoming, envelope))
            {
                observation.IngestionRun = run;
                observations.Add(observation);
            }
        }

        written += await AddNewObservationsAsync(observations, cancellationToken).ConfigureAwait(false);
        CompleteRun(run, written);
        AddArtifactManifests(run, envelopes);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return written;
    }

    private async Task<int> AddNewObservationsAsync(IEnumerable<Observation> observations, CancellationToken cancellationToken)
    {
        var values = observations.DistinctBy(x => x.ContentHash).ToArray();
        if (values.Length == 0) return 0;
        var hashes = values.Select(x => x.ContentHash).ToArray();
        var existing = await dbContext.Observations.Where(x => hashes.Contains(x.ContentHash))
            .Select(x => x.ContentHash).ToListAsync(cancellationToken).ConfigureAwait(false);
        var known = existing.ToHashSet(StringComparer.Ordinal);
        var fresh = values.Where(x => known.Add(x.ContentHash)).ToArray();
        dbContext.Observations.AddRange(fresh);
        return fresh.Length;
    }

    private static IEnumerable<Observation> HistoryObservations(
        HistoricalStart historical,
        ProviderRecentStart incoming,
        SourceEnvelope<ProviderRecentStart> envelope)
    {
        var values = new Dictionary<string, string?>
        {
            ["StartTimeUtc"] = incoming.StartTimeUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            ["TrackName"] = incoming.TrackName,
            ["RaceNumber"] = incoming.RaceNumber.ToString(CultureInfo.InvariantCulture),
            ["DistanceMetres"] = incoming.DistanceMetres.ToString(CultureInfo.InvariantCulture),
            ["StartMethod"] = incoming.StartMethod.ToString(),
            ["PostPosition"] = incoming.PostPosition.ToString(CultureInfo.InvariantCulture),
            ["FinishPosition"] = incoming.FinishPosition?.ToString(CultureInfo.InvariantCulture),
            ["KilometerTimeSeconds"] = incoming.KilometerTimeSeconds?.ToString(CultureInfo.InvariantCulture),
            ["Odds"] = incoming.Odds?.ToString(CultureInfo.InvariantCulture),
            ["Shoes"] = incoming.Shoes,
            ["Sulky"] = incoming.Sulky,
            ["TrackCondition"] = incoming.TrackCondition,
            ["PrizeMoneySek"] = incoming.PrizeMoneySek?.ToString(CultureInfo.InvariantCulture),
            ["Galloped"] = incoming.Galloped.ToString(CultureInfo.InvariantCulture),
            ["RaceComment"] = incoming.RaceComment
        };
        foreach (var value in values)
        {
            var state = value.Value is null ? ObservationState.NotReported : ObservationState.Observed;
            var observation = ObservationFactory.Create("HistoricalStart", historical.Id.ToString("D", CultureInfo.InvariantCulture),
                value.Key, envelope.SourceName, envelope.SourceUrl.ToString(), envelope.RetrievedAtUtc,
                envelope.ObservedAtUtc, value.Value ?? string.Empty, value.Value, IsOfficial(envelope.SourceName), state,
                incoming.StartTimeUtc);
            observation.ContentHash = StableHash($"{historical.Id:D}|{value.Key}|{envelope.SourceName}|{state}|{value.Value}");
            yield return observation;
        }
    }

    private static void ApplyProjection(HistoricalStart target, ProviderRecentStart source, SourceEnvelope<ProviderRecentStart> envelope)
    {
        target.ExternalRaceId = source.ExternalRaceId;
        target.StartTimeUtc = source.StartTimeUtc;
        target.TrackName = source.TrackName;
        target.RaceNumber = source.RaceNumber;
        target.DistanceMetres = source.DistanceMetres;
        target.StartMethod = source.StartMethod;
        target.PostPosition = source.PostPosition;
        target.FinishPosition = source.FinishPosition ?? target.FinishPosition;
        target.KilometerTimeSeconds = source.KilometerTimeSeconds ?? target.KilometerTimeSeconds;
        target.Odds = source.Odds ?? target.Odds;
        target.Shoes = source.Shoes ?? target.Shoes;
        target.Sulky = source.Sulky ?? target.Sulky;
        target.TrackCondition = source.TrackCondition ?? target.TrackCondition;
        target.PrizeMoneySek = source.PrizeMoneySek ?? target.PrizeMoneySek;
        target.Galloped = source.Galloped;
        target.RaceComment = source.RaceComment ?? target.RaceComment;
        target.SourceName = envelope.SourceName;
        target.SourceUrl = envelope.SourceUrl.ToString();
        if (target.RetrievedAtUtc == default || envelope.RetrievedAtUtc < target.RetrievedAtUtc)
            target.RetrievedAtUtc = envelope.RetrievedAtUtc;
        target.ObservedAtUtc ??= envelope.ObservedAtUtc;
    }

    private IngestionRun BeginRun(string provider, int recordsRead)
    {
        var run = new IngestionRun
        {
            Provider = provider,
            StartedAtUtc = DateTimeOffset.UtcNow,
            Status = "Running",
            RecordsRead = recordsRead
        };
        dbContext.IngestionRuns.Add(run);
        return run;
    }

    private static void CompleteRun(IngestionRun run, int written)
    {
        run.CompletedAtUtc = DateTimeOffset.UtcNow;
        run.Status = "Completed";
        run.RecordsWritten = written;
    }

    private void AddArtifactManifests<T>(IngestionRun run, IEnumerable<SourceEnvelope<T>> envelopes)
    {
        foreach (var envelope in envelopes.Where(x => x.RawArtifact is not null).DistinctBy(x => x.RawArtifact!.Sha256))
        {
            dbContext.RawArtifactManifests.Add(new RawArtifactManifest
            {
                IngestionRun = run,
                Provider = envelope.SourceName,
                SourceUrl = envelope.SourceUrl.ToString(),
                RelativePath = envelope.RawArtifact!.RelativePath,
                Sha256 = envelope.RawArtifact.Sha256,
                RetrievedAtUtc = envelope.RetrievedAtUtc,
                SizeBytes = envelope.RawArtifact.SizeBytes
            });
        }
    }

    private static string Completeness(ProviderRecentStart value)
    {
        var missing = new List<string>();
        if (value.FinishPosition is null) missing.Add("FinishPosition");
        if (value.KilometerTimeSeconds is null) missing.Add("KilometerTime");
        if (value.Odds is null) missing.Add("Odds");
        if (value.Shoes is null) missing.Add("Shoes");
        if (value.Sulky is null) missing.Add("Sulky");
        if (value.RaceComment is null) missing.Add("RaceComment");
        return string.Join(',', missing);
    }

    private static void ValidateRecentStarts(IReadOnlyCollection<SourceEnvelope<ProviderRecentStart>> envelopes)
    {
        foreach (var envelope in envelopes)
        {
            var value = envelope.Value;
            if (string.IsNullOrWhiteSpace(value.HorseExternalId) || string.IsNullOrWhiteSpace(value.ExternalRaceId))
                throw new InvalidDataException("Recent starts require stable horse and race identifiers.");
            if (value.StartTimeUtc >= envelope.RetrievedAtUtc)
                throw new InvalidDataException($"Recent start {value.ExternalRaceId} is not in the past at retrieval time.");
            if (value.RaceNumber <= 0 || value.DistanceMetres <= 0 || value.PostPosition < 0)
                throw new InvalidDataException($"Recent start {value.ExternalRaceId} has invalid race, distance, or post-position values.");
        }
        var duplicate = envelopes.GroupBy(x => (x.SourceName, x.Value.HorseExternalId, x.Value.ExternalRaceId))
            .FirstOrDefault(x => x.Select(value => JsonSerializer.Serialize(value.Value)).Distinct(StringComparer.Ordinal).Count() > 1);
        if (duplicate is not null)
            throw new InvalidDataException($"Conflicting duplicate recent start {duplicate.Key.ExternalRaceId} for horse {duplicate.Key.HorseExternalId}.");
    }

    private static int SourcePriority(string source) => source switch
    {
        "SvenskTravsport" => 400,
        "ATG" => 300,
        "Skoinfo" => 200,
        "Travmaskinen" => 100,
        _ => 0
    };

    private static bool IsOfficial(string source) => source is "SvenskTravsport" or "ATG";
    private static string NormalizeKey(string value) => string.Concat(value.Trim().ToLowerInvariant().Where(char.IsLetterOrDigit));
    private static DateTimeOffset Max(DateTimeOffset left, DateTimeOffset right) => left >= right ? left : right;
    private static string StableHash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}

public static class HistoricalStartRevisionFactory
{
    public static HistoricalStartRevision Create(
        HistoricalStart historical,
        ProviderRecentStart value,
        SourceEnvelope<ProviderRecentStart> envelope)
    {
        var payload = JsonSerializer.Serialize(value);
        var canonical = $"{historical.Id:D}|{envelope.SourceName}|{payload}";
        return new HistoricalStartRevision
        {
            HistoricalStart = historical,
            HistoricalStartId = historical.Id,
            HorseId = historical.HorseId,
            DriverId = historical.DriverId,
            TrainerId = historical.TrainerId,
            StartTimeUtc = value.StartTimeUtc,
            TrackName = value.TrackName,
            RaceNumber = value.RaceNumber,
            DistanceMetres = value.DistanceMetres,
            StartMethod = value.StartMethod,
            PostPosition = value.PostPosition,
            FinishPosition = value.FinishPosition,
            KilometerTimeSeconds = value.KilometerTimeSeconds,
            Odds = value.Odds,
            Shoes = value.Shoes,
            Sulky = value.Sulky,
            TrackCondition = value.TrackCondition,
            PrizeMoneySek = value.PrizeMoneySek,
            Galloped = value.Galloped,
            RaceComment = value.RaceComment,
            SourceName = envelope.SourceName,
            SourceUrl = envelope.SourceUrl.ToString(),
            RetrievedAtUtc = envelope.RetrievedAtUtc,
            ObservedAtUtc = envelope.ObservedAtUtc,
            ContentHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant()
        };
    }
}

public sealed class ProviderHealthService(TravDbContext dbContext)
{
    public async Task RecordSuccessAsync(string provider, string scope, DateTimeOffset atUtc, string? cursor, CancellationToken cancellationToken)
    {
        var checkpoint = await GetAsync(provider, scope, cancellationToken).ConfigureAwait(false);
        checkpoint.LastAttemptAtUtc = atUtc;
        checkpoint.LastSuccessAtUtc = atUtc;
        checkpoint.Cursor = cursor;
        checkpoint.ConsecutiveFailures = 0;
        checkpoint.LastError = null;
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task RecordFailureAsync(string provider, string scope, DateTimeOffset atUtc, Exception exception, CancellationToken cancellationToken)
    {
        var checkpoint = await GetAsync(provider, scope, cancellationToken).ConfigureAwait(false);
        checkpoint.LastAttemptAtUtc = atUtc;
        checkpoint.ConsecutiveFailures++;
        checkpoint.LastError = exception.Message;
        dbContext.IngestionRuns.Add(new IngestionRun
        {
            Provider = $"{provider}/{scope}",
            StartedAtUtc = atUtc,
            CompletedAtUtc = atUtc,
            Status = "Failed",
            RecordsRead = 0,
            RecordsWritten = 0,
            Error = exception.Message
        });
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<ProviderCheckpoint> GetAsync(string provider, string scope, CancellationToken cancellationToken)
    {
        var checkpoint = await dbContext.ProviderCheckpoints.SingleOrDefaultAsync(x => x.Provider == provider && x.Scope == scope, cancellationToken).ConfigureAwait(false);
        if (checkpoint is not null) return checkpoint;
        checkpoint = new ProviderCheckpoint { Provider = provider, Scope = scope };
        dbContext.ProviderCheckpoints.Add(checkpoint);
        return checkpoint;
    }
}
