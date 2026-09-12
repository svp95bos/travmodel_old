using TravModel.Domain;

namespace TravModel.Application;

public sealed record SourceEnvelope<T>(
    T Value,
    string SourceName,
    Uri SourceUrl,
    DateTimeOffset RetrievedAtUtc,
    DateTimeOffset? ObservedAtUtc,
    string RawPayload,
    RawArtifactReference? RawArtifact = null);

public sealed record RawArtifactReference(string RelativePath, string Sha256, long SizeBytes);

public sealed record ProviderMeeting(
    string ExternalId,
    string TrackExternalId,
    string TrackName,
    DateOnly Date,
    IReadOnlyList<ProviderRace> Races,
    double? TrackLatitude = null,
    double? TrackLongitude = null);

public sealed record ProviderRace(
    string ExternalId,
    int RaceNumber,
    DateTimeOffset ScheduledStartUtc,
    int DistanceMetres,
    StartMethod StartMethod,
    string RaceType,
    string ConditionsText,
    IReadOnlyList<ProviderStarter> Starters);

public sealed record ProviderStarter(
    string HorseExternalId,
    string HorseName,
    string DriverExternalId,
    string DriverName,
    string TrainerExternalId,
    string TrainerName,
    int HorseNumber,
    int PostPosition,
    int DistanceMetres,
    bool IsScratched);

public sealed record ProviderResult(
    string RaceExternalId,
    string HorseExternalId,
    int? FinishPosition,
    string FinishCode,
    decimal? KilometerTimeSeconds,
    decimal? WinningMarginMetres,
    decimal? PrizeMoneySek,
    bool Galloped,
    string? RaceComment);

public interface IRaceProvider
{
    string Name { get; }

    Task<IReadOnlyList<SourceEnvelope<ProviderMeeting>>> GetMeetingsAsync(
        DateOnly from,
        DateOnly toDate,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<SourceEnvelope<ProviderResult>>> GetResultsAsync(
        DateOnly from,
        DateOnly toDate,
        CancellationToken cancellationToken);
}

public interface IBettingMarketProvider
{
    string Name { get; }

    Task<BettingMarketSnapshot> GetMarketSnapshotAsync(
        IReadOnlyCollection<Race> races,
        DateTimeOffset asOfUtc,
        CancellationToken cancellationToken);
}

public sealed record BettingMarketSnapshot(
    IReadOnlyList<Observation> Observations,
    IReadOnlyList<BettingMarketMembership> Memberships);

public interface IWeatherProvider
{
    string Name { get; }

    Task<IReadOnlyList<Observation>> GetWeatherObservationsAsync(
        IReadOnlyCollection<Race> races,
        DateTimeOffset asOfUtc,
        CancellationToken cancellationToken);
}

public interface ITravRepository
{
    Task<IReadOnlyList<Race>> GetRacesAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken);

    Task<RaceFeatureInput?> GetFeatureInputAsync(
        Guid raceId,
        DateTimeOffset cutoffUtc,
        CancellationToken cancellationToken);

    Task AppendObservationsAsync(
        IReadOnlyCollection<Observation> observations,
        CancellationToken cancellationToken);

    Task SaveBettingMarketMembershipsAsync(
        IReadOnlyCollection<BettingMarketMembership> memberships,
        CancellationToken cancellationToken);

    Task SavePredictionRunAsync(PredictionRun run, CancellationToken cancellationToken);
}

public interface IWinProbabilityModel
{
    Guid ModelVersionId { get; }
    ModelKind Kind { get; }
    IReadOnlyList<StarterProbability> Predict(RaceFeatureSet race);
}

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
