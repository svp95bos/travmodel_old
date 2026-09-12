# SMHI Open Data adapters

The infrastructure project integrates two official, unauthenticated SMHI APIs.

## Forecasts: SNOW1gv1

Point forecasts are requested from:

```text
https://opendata-download-metfcst.smhi.se/api/category/snow1g/version/1/
  geotype/point/lon/{longitude}/lat/{latitude}/data.json
```

`SmhiSnowForecastProvider` selects the time step closest to the scheduled race start and maps:

| SMHI parameter | Canonical observation field |
|---|---|
| `air_temperature` | `ForecastTemperature` |
| `wind_from_direction` | `ForecastWindDirection` |
| `wind_speed` | `ForecastWindSpeed` |
| `relative_humidity` | `ForecastHumidity` |
| `visibility_in_air` | `ForecastVisibilityKilometres` |
| `precipitation_amount_mean_deterministic` | `ForecastPrecipitationAmount` |

The precipitation accumulation interval is stored separately as
`ForecastPrecipitationIntervalHours`. `ObservedAtUtc` is the model `referenceTime`, while
`ValidAtUtc` is the forecast time. A response whose model reference time is later than the
requested cutoff is rejected.

SNOW1gv1 replaced PMP3gv2, which SMHI retired on 31 March 2026.

## Measurements: MetObs

Station catalogues are requested by parameter:

```text
https://opendata-download-metobs.smhi.se/api/version/latest/
  parameter/{parameterId}/station.json
```

The latest hourly value is then requested from the nearest active station within 100 km:

```text
https://opendata-download-metobs.smhi.se/api/version/latest/
  parameter/{parameterId}/station/{stationId}/period/latest-hour/data.json
```

| Parameter ID | SMHI measurement | Canonical observation field |
|---:|---|---|
| 1 | hourly instantaneous air temperature | `Temperature` |
| 3 | hourly 10-minute mean wind direction | `WindDirection` |
| 4 | hourly 10-minute mean wind speed | `WindSpeed` |
| 6 | hourly instantaneous relative humidity | `Humidity` |
| 7 | one-hour precipitation sum | `Precipitation1h` |
| 12 | hourly instantaneous visibility | `VisibilityMetres` |

The raw observation retains SMHI's quality code, station ID/name, and calculated station
distance. Only measurements timestamped at or before the requested cutoff are accepted.

Neither adapter derives track condition from weather.
