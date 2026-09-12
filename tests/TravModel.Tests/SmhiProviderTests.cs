using System.Globalization;
using System.Net;
using System.Text;
using TravModel.Domain;
using TravModel.Infrastructure;

namespace TravModel.Tests;

public sealed class SmhiProviderTests
{
    [Fact]
    public async Task SnowProvider_MapsForecastAndPreservesReferenceAndValidTimes()
    {
        const string json = """
            {
              "referenceTime":"2026-09-12T12:00:00Z",
              "timeSeries":[{
                "time":"2026-09-12T14:00:00Z",
                "intervalParametersStartTime":"2026-09-12T13:00:00Z",
                "data":{
                  "air_temperature":14.2,
                  "wind_from_direction":225,
                  "wind_speed":4.1,
                  "relative_humidity":71,
                  "visibility_in_air":18.5,
                  "precipitation_amount_mean_deterministic":0.3
                }
              }]
            }
            """;
        var handler = new StubHandler(_ => Json(json));
        var provider = new SmhiSnowForecastProvider(new HttpClient(handler), Options());
        var race = LocatedRace(new DateTimeOffset(2026, 9, 12, 14, 5, 0, TimeSpan.Zero));

        var observations = await provider.GetWeatherObservationsAsync(
            [race],
            new DateTimeOffset(2026, 9, 12, 12, 30, 0, TimeSpan.Zero),
            CancellationToken.None);

        var temperature = Assert.Single(observations, x => x.Field == "ForecastTemperature");
        Assert.Equal("14.2", temperature.NormalizedValue);
        Assert.Equal(DateTimeOffset.Parse("2026-09-12T12:00:00Z", CultureInfo.InvariantCulture), temperature.ObservedAtUtc);
        Assert.Equal(DateTimeOffset.Parse("2026-09-12T14:00:00Z", CultureInfo.InvariantCulture), temperature.ValidAtUtc);
        Assert.Contains("/snow1g/version/1/geotype/point/lon/18.1/lat/59.2/data.json", temperature.SourceUrl, StringComparison.Ordinal);
        Assert.Equal(7, observations.Count);
    }

    [Fact]
    public async Task SnowProvider_RejectsModelRunFromAfterPointInTimeCutoff()
    {
        const string json = """
            {"referenceTime":"2026-09-12T13:00:00Z","timeSeries":[]}
            """;
        var provider = new SmhiSnowForecastProvider(new HttpClient(new StubHandler(_ => Json(json))), Options());

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => provider.GetWeatherObservationsAsync(
            [LocatedRace(new DateTimeOffset(2026, 9, 12, 14, 0, 0, TimeSpan.Zero))],
            new DateTimeOffset(2026, 9, 12, 12, 30, 0, TimeSpan.Zero),
            CancellationToken.None));

        Assert.Contains("after the requested point-in-time", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MetObsProvider_SelectsNearestActiveStationAndRetainsQuality()
    {
        var handler = new StubHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("station.json", StringComparison.Ordinal))
            {
                return Json("""
                    {"station":[
                      {"key":"far","name":"Far","latitude":60.0,"longitude":19.0,"active":true},
                      {"key":"near","name":"Near","latitude":59.21,"longitude":18.11,"active":true},
                      {"key":"inactive","name":"Inactive","latitude":59.2,"longitude":18.1,"active":false}
                    ]}
                    """);
            }

            Assert.Contains("/station/near/period/latest-hour/data.json", path, StringComparison.Ordinal);
            return Json("""
                {"value":[{"date":1789221600000,"value":"11.5","quality":"G"}]}
                """);
        });
        var provider = new SmhiMetObservationsProvider(new HttpClient(handler), Options());
        var asOf = DateTimeOffset.FromUnixTimeMilliseconds(1789221600000);

        var observations = await provider.GetWeatherObservationsAsync(
            [LocatedRace(asOf.AddHours(2))], asOf, CancellationToken.None);

        Assert.Equal(6, observations.Count);
        var temperature = Assert.Single(observations, x => x.Field == "Temperature");
        Assert.Equal("11.5", temperature.NormalizedValue);
        Assert.Contains("\"quality\":\"G\"", temperature.RawValue, StringComparison.Ordinal);
        Assert.Contains("\"stationId\":\"near\"", temperature.RawValue, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SmhiProviders_RequireTrackCoordinates()
    {
        var race = TestData.Race(DateTimeOffset.UtcNow.AddHours(1));
        var provider = new SmhiSnowForecastProvider(new HttpClient(new StubHandler(_ => Json("{}"))), Options());

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => provider.GetWeatherObservationsAsync(
            [race], DateTimeOffset.UtcNow, CancellationToken.None));

        Assert.Contains("latitude/longitude", exception.Message, StringComparison.Ordinal);
    }

    private static Race LocatedRace(DateTimeOffset start)
    {
        var race = TestData.Race(start);
        race.Meeting = new Meeting
        {
            ExternalSource = "test",
            ExternalId = "meeting",
            MeetingDate = DateOnly.FromDateTime(start.UtcDateTime),
            Track = new Track
            {
                ExternalSource = "test",
                ExternalId = "track",
                Name = "Test track",
                Latitude = 59.2,
                Longitude = 18.1
            }
        };
        return race;
    }

    private static SmhiOpenDataOptions Options() => new()
    {
        ForecastBaseUri = new Uri("https://forecast.test/api/category/snow1g/version/1/"),
        ObservationsBaseUri = new Uri("https://observations.test/api/version/latest/")
    };

    private static HttpResponseMessage Json(string value) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(value, Encoding.UTF8, "application/json")
    };

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(responder(request));
    }
}
