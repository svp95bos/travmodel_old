using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using TravModel.Application;
using TravModel.Domain;

namespace TravModel.Infrastructure;

public sealed class SmhiOpenDataOptions
{
    public Uri ForecastBaseUri { get; init; } = new("https://opendata-download-metfcst.smhi.se/api/category/snow1g/version/1/");
    public Uri ObservationsBaseUri { get; init; } = new("https://opendata-download-metobs.smhi.se/api/version/latest/");
    public TimeSpan MaximumForecastOffset { get; init; } = TimeSpan.FromHours(6);
    public double MaximumStationDistanceKilometres { get; init; } = 100;
}

/// <summary>Combines current SNOW forecasts with latest measured station observations.</summary>
public sealed class SmhiWeatherProvider : IWeatherProvider
{
    private readonly SmhiSnowForecastProvider forecastProvider;
    private readonly SmhiMetObservationsProvider observationsProvider;

    public SmhiWeatherProvider(HttpClient httpClient, SmhiOpenDataOptions? options = null)
    {
        var resolved = options ?? new SmhiOpenDataOptions();
        forecastProvider = new SmhiSnowForecastProvider(httpClient, resolved);
        observationsProvider = new SmhiMetObservationsProvider(httpClient, resolved);
    }

    public string Name => "SMHI";

    public async Task<IReadOnlyList<Observation>> GetWeatherObservationsAsync(
        IReadOnlyCollection<Race> races,
        DateTimeOffset asOfUtc,
        CancellationToken cancellationToken)
    {
        var forecast = await forecastProvider.GetWeatherObservationsAsync(races, asOfUtc, cancellationToken).ConfigureAwait(false);
        var measured = await observationsProvider.GetWeatherObservationsAsync(races, asOfUtc, cancellationToken).ConfigureAwait(false);
        return [.. forecast, .. measured];
    }
}

/// <summary>Adapter for SMHI's current SNOW1gv1 point forecast API.</summary>
public sealed class SmhiSnowForecastProvider(HttpClient httpClient, SmhiOpenDataOptions options) : IWeatherProvider
{
    private static readonly Dictionary<string, string> FieldMap = new(StringComparer.Ordinal)
    {
        ["air_temperature"] = "ForecastTemperature",
        ["wind_from_direction"] = "ForecastWindDirection",
        ["wind_speed"] = "ForecastWindSpeed",
        ["relative_humidity"] = "ForecastHumidity",
        ["visibility_in_air"] = "ForecastVisibilityKilometres",
        ["precipitation_amount_mean_deterministic"] = "ForecastPrecipitationAmount"
    };

    public string Name => "SMHI/SNOW1gv1";

    public async Task<IReadOnlyList<Observation>> GetWeatherObservationsAsync(
        IReadOnlyCollection<Race> races,
        DateTimeOffset asOfUtc,
        CancellationToken cancellationToken)
    {
        var located = SmhiRaceLocation.Require(races);
        var output = new List<Observation>();
        foreach (var group in located.GroupBy(x => (x.Latitude, x.Longitude)))
        {
            var uri = BuildUri(group.Key.Latitude, group.Key.Longitude);
            var response = await httpClient.GetFromJsonAsync<SmhiSnowResponse>(uri, SerializerOptions, cancellationToken)
                .ConfigureAwait(false) ?? throw new InvalidDataException("SMHI returned an empty SNOW forecast response.");
            if (response.ReferenceTime > asOfUtc)
                throw new InvalidDataException($"SMHI forecast reference time {response.ReferenceTime:O} is after the requested point-in-time {asOfUtc:O}.");

            foreach (var item in group)
            {
                var step = response.TimeSeries
                    .OrderBy(x => Duration(x.Time, item.Race.ScheduledStartUtc))
                    .FirstOrDefault();
                if (step is null || Duration(step.Time, item.Race.ScheduledStartUtc) > options.MaximumForecastOffset)
                    continue;

                foreach (var parameter in step.Data.Where(x => FieldMap.ContainsKey(x.Key)))
                {
                    if (parameter.Value.ValueKind != JsonValueKind.Number) continue;
                    var raw = parameter.Value.GetRawText();
                    output.Add(ObservationFactory.Create(
                        "Race",
                        item.Race.Id.ToString("D", CultureInfo.InvariantCulture),
                        FieldMap[parameter.Key],
                        Name,
                        uri.ToString(),
                        asOfUtc,
                        response.ReferenceTime,
                        raw,
                        raw,
                        true,
                        validAtUtc: step.Time));
                }

                var intervalHours = (step.Time - step.IntervalParametersStartTime).TotalHours;
                output.Add(ObservationFactory.Create(
                    "Race",
                    item.Race.Id.ToString("D", CultureInfo.InvariantCulture),
                    "ForecastPrecipitationIntervalHours",
                    Name,
                    uri.ToString(),
                    asOfUtc,
                    response.ReferenceTime,
                    intervalHours.ToString("0.###", CultureInfo.InvariantCulture),
                    intervalHours.ToString("0.###", CultureInfo.InvariantCulture),
                    true,
                    validAtUtc: step.Time));
            }
        }

        return output;
    }

    private Uri BuildUri(double latitude, double longitude)
    {
        var path = FormattableString.Invariant(
            $"geotype/point/lon/{longitude:0.######}/lat/{latitude:0.######}/data.json");
        return new Uri(options.ForecastBaseUri, path);
    }

    private static TimeSpan Duration(DateTimeOffset left, DateTimeOffset right) => (left - right).Duration();

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
}

/// <summary>Adapter for SMHI's hourly MetObs station API.</summary>
public sealed class SmhiMetObservationsProvider(HttpClient httpClient, SmhiOpenDataOptions options) : IWeatherProvider
{
    private static readonly SmhiMetParameter[] Parameters =
    [
        new("1", "Temperature"),
        new("3", "WindDirection"),
        new("4", "WindSpeed"),
        new("6", "Humidity"),
        new("7", "Precipitation1h"),
        new("12", "VisibilityMetres")
    ];

    public string Name => "SMHI/MetObs";

    public async Task<IReadOnlyList<Observation>> GetWeatherObservationsAsync(
        IReadOnlyCollection<Race> races,
        DateTimeOffset asOfUtc,
        CancellationToken cancellationToken)
    {
        var located = SmhiRaceLocation.Require(races);
        var catalogs = new Dictionary<string, SmhiStation[]>();
        foreach (var parameter in Parameters)
        {
            var uri = new Uri(options.ObservationsBaseUri, $"parameter/{parameter.Id}/station.json");
            var catalogue = await httpClient.GetFromJsonAsync<SmhiStationCatalogue>(uri, cancellationToken)
                .ConfigureAwait(false) ?? throw new InvalidDataException($"SMHI returned an empty station catalogue for parameter {parameter.Id}.");
            catalogs[parameter.Id] = catalogue.Stations.Where(x => x.Active).ToArray();
        }

        var output = new List<Observation>();
        foreach (var group in located.GroupBy(x => (x.Latitude, x.Longitude)))
        {
            foreach (var parameter in Parameters)
            {
                var station = catalogs[parameter.Id]
                    .Select(x => new { Station = x, Distance = Haversine(group.Key.Latitude, group.Key.Longitude, x.Latitude, x.Longitude) })
                    .Where(x => x.Distance <= options.MaximumStationDistanceKilometres)
                    .OrderBy(x => x.Distance)
                    .FirstOrDefault();
                if (station is null) continue;

                var uri = new Uri(options.ObservationsBaseUri,
                    $"parameter/{parameter.Id}/station/{Uri.EscapeDataString(station.Station.Key)}/period/latest-hour/data.json");
                var response = await httpClient.GetFromJsonAsync<SmhiObservationResponse>(uri, cancellationToken)
                    .ConfigureAwait(false) ?? throw new InvalidDataException($"SMHI returned empty observation data for station {station.Station.Key}.");
                var value = response.Values
                    .Where(x => x.Date <= asOfUtc.ToUnixTimeMilliseconds())
                    .OrderByDescending(x => x.Date)
                    .FirstOrDefault();
                if (value is null) continue;

                var observedAt = DateTimeOffset.FromUnixTimeMilliseconds(value.Date);
                var raw = JsonSerializer.Serialize(new
                {
                    value = value.Value,
                    quality = value.Quality,
                    stationId = station.Station.Key,
                    stationName = station.Station.Name,
                    distanceKilometres = Math.Round(station.Distance, 3)
                });
                foreach (var item in group)
                {
                    output.Add(ObservationFactory.Create(
                        "Race",
                        item.Race.Id.ToString("D", CultureInfo.InvariantCulture),
                        parameter.Field,
                        Name,
                        uri.ToString(),
                        asOfUtc,
                        observedAt,
                        raw,
                        Normalize(value.Value),
                        true));
                }
            }
        }

        return output;
    }

    private static string? Normalize(string value) =>
        decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed.ToString(CultureInfo.InvariantCulture)
            : null;

    private static double Haversine(double latitude1, double longitude1, double latitude2, double longitude2)
    {
        const double earthRadiusKilometres = 6371.0088;
        var latitudeDelta = DegreesToRadians(latitude2 - latitude1);
        var longitudeDelta = DegreesToRadians(longitude2 - longitude1);
        var a = Math.Pow(Math.Sin(latitudeDelta / 2), 2)
            + Math.Cos(DegreesToRadians(latitude1)) * Math.Cos(DegreesToRadians(latitude2))
            * Math.Pow(Math.Sin(longitudeDelta / 2), 2);
        return earthRadiusKilometres * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private static double DegreesToRadians(double value) => value * Math.PI / 180;
}

internal static class SmhiRaceLocation
{
    public static SmhiLocatedRace[] Require(IEnumerable<Race> races)
    {
        var output = new List<SmhiLocatedRace>();
        foreach (var race in races)
        {
            var track = race.Meeting?.Track;
            if (track?.Latitude is null || track.Longitude is null)
                throw new InvalidDataException($"Race {race.ExternalId} has no track latitude/longitude; SMHI lookup cannot be reproduced.");
            if (track.Latitude is < -90 or > 90 || track.Longitude is < -180 or > 180)
                throw new InvalidDataException($"Race {race.ExternalId} has invalid track coordinates.");
            output.Add(new SmhiLocatedRace(race, track.Latitude.Value, track.Longitude.Value));
        }

        return [.. output];
    }
}

internal sealed record SmhiLocatedRace(Race Race, double Latitude, double Longitude);
internal sealed record SmhiMetParameter(string Id, string Field);

internal sealed class SmhiSnowResponse
{
    public DateTimeOffset ReferenceTime { get; init; }
    public List<SmhiSnowTimeStep> TimeSeries { get; init; } = [];
}

internal sealed class SmhiSnowTimeStep
{
    public DateTimeOffset Time { get; init; }
    public DateTimeOffset IntervalParametersStartTime { get; init; }
    public Dictionary<string, JsonElement> Data { get; init; } = [];
}

internal sealed class SmhiStationCatalogue
{
    [JsonPropertyName("station")]
    public List<SmhiStation> Stations { get; init; } = [];
}

internal sealed class SmhiStation
{
    public string Key { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public bool Active { get; init; }
}

internal sealed class SmhiObservationResponse
{
    [JsonPropertyName("value")]
    public List<SmhiObservationValue> Values { get; init; } = [];
}

internal sealed class SmhiObservationValue
{
    public long Date { get; init; }
    public string Value { get; init; } = string.Empty;
    public string Quality { get; init; } = string.Empty;
}
