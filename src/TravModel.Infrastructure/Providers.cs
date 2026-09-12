using System.Globalization;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TravModel.Application;
using TravModel.Domain;

namespace TravModel.Infrastructure;

public sealed class ProviderAccessOptions
{
    public bool Enabled { get; init; }
    public bool AuthorizationConfirmed { get; init; }
    public Uri? BaseUri { get; init; }
}

public sealed class SvenskTravsportProvider(
    HttpClient httpClient,
    ProviderAccessOptions options,
    RawArtifactStore artifactStore) : IRaceProvider
{
    public string Name => "SvenskTravsport";

    public Task<IReadOnlyList<SourceEnvelope<ProviderMeeting>>> GetMeetingsAsync(
        DateOnly from,
        DateOnly toDate,
        CancellationToken cancellationToken) =>
        GetAsync<ProviderMeeting>($"meetings?from={from:yyyy-MM-dd}&to={toDate:yyyy-MM-dd}", cancellationToken);

    public Task<IReadOnlyList<SourceEnvelope<ProviderResult>>> GetResultsAsync(
        DateOnly from,
        DateOnly toDate,
        CancellationToken cancellationToken) =>
        GetAsync<ProviderResult>($"results?from={from:yyyy-MM-dd}&to={toDate:yyyy-MM-dd}", cancellationToken);

    private async Task<IReadOnlyList<SourceEnvelope<T>>> GetAsync<T>(string relativeUrl, CancellationToken cancellationToken)
    {
        EnsureAuthorized();
        var sourceUrl = new Uri(options.BaseUri!, relativeUrl);
        using var response = await httpClient.GetAsync(sourceUrl, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        var retrieved = DateTimeOffset.UtcNow;
        var artifact = await artifactStore.StoreAsync(Name, retrieved, bytes, cancellationToken).ConfigureAwait(false);
        var values = JsonSerializer.Deserialize<List<T>>(bytes, JsonOptions) ?? [];
        var raw = Encoding.UTF8.GetString(bytes);
        var reference = new RawArtifactReference(artifact.RelativePath, artifact.Sha256, artifact.SizeBytes);
        return values.Select(value => new SourceEnvelope<T>(value, Name, sourceUrl, retrieved, null, raw, reference)).ToArray();
    }

    private void EnsureAuthorized()
    {
        if (!options.Enabled || !options.AuthorizationConfirmed || options.BaseUri is null)
        {
            throw new InvalidOperationException(
                "Svensk Travsport bulk retrieval is disabled. Configure an authorized export/gateway and explicitly confirm authorization.");
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}

public sealed record MarketObservationDto(
    string RaceExternalId,
    string HorseExternalId,
    string Field,
    string RawValue,
    string? NormalizedValue,
    DateTimeOffset? ObservedAtUtc,
    string SourceUrl);

public sealed record MarketMembershipDto(string RaceExternalId, string Product, string ProductId, int LegNumber);

public sealed record AtgMarketSnapshotDto(
    IReadOnlyList<MarketObservationDto> Observations,
    IReadOnlyList<MarketMembershipDto> Memberships);

public sealed class AtgMarketProvider(HttpClient httpClient, ProviderAccessOptions options) : IBettingMarketProvider
{
    public string Name => "ATG";

    public async Task<BettingMarketSnapshot> GetMarketSnapshotAsync(
        IReadOnlyCollection<Race> races,
        DateTimeOffset asOfUtc,
        CancellationToken cancellationToken)
    {
        EnsureAuthorized();
        if (races.Count == 0) return new BettingMarketSnapshot([], []);
        var ids = string.Join(',', races.Select(x => Uri.EscapeDataString(x.ExternalId)));
        var uri = new Uri(options.BaseUri!, $"market?raceIds={ids}&asOf={Uri.EscapeDataString(asOfUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture))}");
        var snapshot = await httpClient.GetFromJsonAsync<AtgMarketSnapshotDto>(uri, cancellationToken).ConfigureAwait(false)
            ?? new AtgMarketSnapshotDto([], []);
        var raceMap = races.ToDictionary(x => x.ExternalId, StringComparer.Ordinal);
        var observations = snapshot.Observations.Select(x =>
        {
            if (!raceMap.TryGetValue(x.RaceExternalId, out var race))
                throw new InvalidDataException($"ATG returned unknown race {x.RaceExternalId}.");
            var starter = race.Starters.SingleOrDefault(s => string.Equals(s.Horse?.ExternalId, x.HorseExternalId, StringComparison.Ordinal))
                ?? throw new InvalidDataException($"ATG returned unknown horse {x.HorseExternalId} in race {x.RaceExternalId}.");
            return ObservationFactory.Create("Starter", starter.Id.ToString("D", CultureInfo.InvariantCulture), x.Field, Name, x.SourceUrl,
                asOfUtc, x.ObservedAtUtc, x.RawValue, x.NormalizedValue, true);
        }).ToArray();
        var memberships = snapshot.Memberships.Select(x =>
        {
            if (!raceMap.TryGetValue(x.RaceExternalId, out var race))
                throw new InvalidDataException($"ATG returned membership for unknown race {x.RaceExternalId}.");
            return new BettingMarketMembership
            {
                RaceId = race.Id,
                Product = x.Product,
                ProductId = x.ProductId,
                LegNumber = x.LegNumber
            };
        }).ToArray();
        return new BettingMarketSnapshot(observations, memberships);
    }

    private void EnsureAuthorized()
    {
        if (!options.Enabled || !options.AuthorizationConfirmed || options.BaseUri is null)
        {
            throw new InvalidOperationException(
                "ATG market retrieval is disabled. Configure an authorized export/gateway and explicitly confirm authorization.");
        }
    }
}

public static class ObservationFactory
{
    public static Observation Create(
        string entityType,
        string entityId,
        string field,
        string sourceName,
        string sourceUrl,
        DateTimeOffset retrievedAtUtc,
        DateTimeOffset? observedAtUtc,
        string rawValue,
        string? normalizedValue,
        bool isAuthoritative,
        ObservationState state = ObservationState.Observed,
        DateTimeOffset? validAtUtc = null)
    {
        var canonical = string.Join('|', entityType, entityId, field, sourceName, sourceUrl,
            retrievedAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            observedAtUtc?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            validAtUtc?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture), rawValue, normalizedValue, state);
        return new Observation
        {
            EntityType = entityType,
            EntityId = entityId,
            Field = field,
            SourceName = sourceName,
            SourceUrl = sourceUrl,
            RetrievedAtUtc = retrievedAtUtc,
            ObservedAtUtc = observedAtUtc,
            ValidAtUtc = validAtUtc,
            RawValue = rawValue,
            NormalizedValue = normalizedValue,
            State = state,
            IsAuthoritative = isAuthoritative,
            ContentHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant()
        };
    }
}
