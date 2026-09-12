using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using TravModel.Domain;

namespace TravModel.Application;

public sealed record RaceFeatureInput(
    Race Race,
    IReadOnlyList<StarterFeatureHistory> Starters,
    IReadOnlyList<Observation> Observations);

public sealed record StarterFeatureHistory(
    Starter Starter,
    IReadOnlyList<HistoricalStart> HistoricalStarts,
    RollingRate Driver30Days,
    RollingRate Trainer30Days);

public sealed record RollingRate(int Starts, int Wins, int Places)
{
    public float WinRate => Starts == 0 ? float.NaN : (float)Wins / Starts;
    public float PlaceRate => Starts == 0 ? float.NaN : (float)Places / Starts;
}

public sealed record RaceFeatureSet(
    Guid RaceId,
    DateTimeOffset CutoffUtc,
    string DatasetFingerprint,
    IReadOnlyList<StarterFeatureVector> Starters);

public sealed record StarterFeatureVector(
    Guid StarterId,
    bool IsScratched,
    float HorseNumber,
    float PostPosition,
    float DistanceMetres,
    float FieldSize,
    float RecentStarts,
    float RecentWinRate,
    float RecentPlaceRate,
    float RecentGallopRate,
    float MeanKilometerTimeSeconds,
    float DriverWinRate30Days,
    float DriverStarts30Days,
    float TrainerWinRate30Days,
    float TrainerStarts30Days,
    float BettingPercentage,
    float MarketOdds,
    float TemperatureCelsius,
    float WindSpeedMetresPerSecond);

public sealed record StarterProbability(Guid StarterId, double Probability, int Rank);

public sealed record StarterShadowFeatures(
    Guid StarterId,
    int RecentStartsAvailable,
    bool HasThreeRecentStarts,
    bool HasFiveRecentStarts,
    string? ShoesToday,
    string? ShoesPreviousStart,
    bool? ShoesChanged,
    string? SulkyToday,
    string? SulkyPreviousStart,
    bool? SulkyChanged);

public sealed record ShadowRaceFeatureSet(
    Guid RaceId,
    DateTimeOffset CutoffUtc,
    IReadOnlyList<StarterShadowFeatures> Starters);

public static class PointInTimeShadowFeatureBuilder
{
    public static ShadowRaceFeatureSet Build(RaceFeatureInput input, DateTimeOffset cutoffUtc)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (cutoffUtc >= input.Race.ScheduledStartUtc)
            throw new ArgumentOutOfRangeException(nameof(cutoffUtc), "Feature cutoff must precede race start.");
        var observations = input.Observations.Where(x => x.RetrievedAtUtc <= cutoffUtc &&
            (x.ObservedAtUtc is null || x.ObservedAtUtc <= cutoffUtc)).ToArray();
        var values = input.Starters.Select(value =>
        {
            var history = value.HistoricalStarts.Where(x => x.StartTimeUtc < cutoffUtc && x.RetrievedAtUtc <= cutoffUtc)
                .OrderByDescending(x => x.StartTimeUtc).Take(10).ToArray();
            var previous = history.FirstOrDefault();
            var shoes = LatestText(observations, value.Starter.Id, "Shoes")
                ?? CombineShoes(LatestText(observations, value.Starter.Id, "ShoesFront"), LatestText(observations, value.Starter.Id, "ShoesHind"));
            var sulky = LatestText(observations, value.Starter.Id, "SulkyType")
                ?? LatestText(observations, value.Starter.Id, "Sulky");
            return new StarterShadowFeatures(value.Starter.Id, history.Length, history.Length >= 3, history.Length >= 5,
                shoes, previous?.Shoes, Changed(shoes, previous?.Shoes), sulky, previous?.Sulky, Changed(sulky, previous?.Sulky));
        }).ToArray();
        return new ShadowRaceFeatureSet(input.Race.Id, cutoffUtc, values);
    }

    private static string? LatestText(IEnumerable<Observation> observations, Guid entityId, string field) => observations
        .Where(x => x.EntityId == entityId.ToString("D", CultureInfo.InvariantCulture) &&
                    string.Equals(x.Field, field, StringComparison.OrdinalIgnoreCase) && x.State == ObservationState.Observed)
        .OrderByDescending(x => x.ObservedAtUtc ?? x.RetrievedAtUtc)
        .Select(x => x.NormalizedValue)
        .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

    private static string? CombineShoes(string? front, string? hind) => front is null && hind is null ? null : $"{front ?? "Unknown"}/{hind ?? "Unknown"}";
    private static bool? Changed(string? current, string? previous) => current is null || previous is null
        ? null
        : !string.Equals(current, previous, StringComparison.OrdinalIgnoreCase);
}

public static class PointInTimeFeatureBuilder
{
    private static readonly TimeSpan RecentWindow = TimeSpan.FromDays(90);

    public static RaceFeatureSet Build(RaceFeatureInput input, DateTimeOffset cutoffUtc)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (cutoffUtc >= input.Race.ScheduledStartUtc)
        {
            throw new ArgumentOutOfRangeException(nameof(cutoffUtc), "Feature cutoff must precede race start.");
        }

        var eligibleObservations = input.Observations
            .Where(x => x.RetrievedAtUtc <= cutoffUtc && (x.ObservedAtUtc is null || x.ObservedAtUtc <= cutoffUtc))
            .ToArray();
        var fieldSize = input.Starters.Count(x => !x.Starter.IsScratched);
        var vectors = input.Starters.Select(x => BuildStarter(x, eligibleObservations, fieldSize, cutoffUtc)).ToArray();
        return new RaceFeatureSet(input.Race.Id, cutoffUtc, Fingerprint(input.Race.Id, cutoffUtc, vectors), vectors);
    }

    private static StarterFeatureVector BuildStarter(
        StarterFeatureHistory input,
        IReadOnlyCollection<Observation> observations,
        int fieldSize,
        DateTimeOffset cutoffUtc)
    {
        var starts = input.HistoricalStarts
            .Where(x => x.StartTimeUtc < cutoffUtc && x.StartTimeUtc >= cutoffUtc - RecentWindow)
            .OrderByDescending(x => x.StartTimeUtc)
            .Take(10)
            .ToArray();

        var completed = starts.Where(x => x.FinishPosition.HasValue).ToArray();
        var timed = starts.Where(x => x.KilometerTimeSeconds.HasValue).ToArray();
        return new StarterFeatureVector(
            input.Starter.Id,
            input.Starter.IsScratched,
            input.Starter.HorseNumber,
            input.Starter.PostPosition,
            input.Starter.DistanceMetres,
            fieldSize,
            starts.Length,
            Rate(completed.Count(x => x.FinishPosition == 1), completed.Length),
            Rate(completed.Count(x => x.FinishPosition <= 3), completed.Length),
            Rate(starts.Count(x => x.Galloped), starts.Length),
            timed.Length == 0 ? float.NaN : (float)timed.Average(x => x.KilometerTimeSeconds!.Value),
            input.Driver30Days.WinRate,
            input.Driver30Days.Starts,
            input.Trainer30Days.WinRate,
            input.Trainer30Days.Starts,
            LatestFloat(observations, input.Starter.Id, "BettingPercentage"),
            LatestFloat(observations, input.Starter.Id, "Odds"),
            LatestFloat(observations, input.Starter.RaceId, "Temperature"),
            LatestFloat(observations, input.Starter.RaceId, "WindSpeed"));
    }

    private static float Rate(int numerator, int denominator) => denominator == 0 ? float.NaN : (float)numerator / denominator;

    private static float LatestFloat(IReadOnlyCollection<Observation> observations, Guid entityId, string field)
    {
        var raw = observations
            .Where(x => x.EntityId == entityId.ToString("D", CultureInfo.InvariantCulture) &&
                        string.Equals(x.Field, field, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.ObservedAtUtc ?? x.RetrievedAtUtc)
            .Select(x => x.NormalizedValue)
            .FirstOrDefault();
        return float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : float.NaN;
    }

    private static string Fingerprint(Guid raceId, DateTimeOffset cutoffUtc, IEnumerable<StarterFeatureVector> vectors)
    {
        var canonical = new StringBuilder()
            .Append(raceId.ToString("D", CultureInfo.InvariantCulture)).Append('|')
            .Append(cutoffUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        foreach (var vector in vectors.OrderBy(x => x.StarterId))
        {
            canonical.Append('|').Append(System.Text.Json.JsonSerializer.Serialize(vector, FingerprintJsonOptions));
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString()))).ToLowerInvariant();
    }

    private static readonly System.Text.Json.JsonSerializerOptions FingerprintJsonOptions = new()
    {
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
    };
}
