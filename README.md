# TravModel

A point-in-time-correct Swedish harness-racing data and win-probability platform.

## Principles

- The race is the sporting entity; betting products are market memberships.
- Changing values are append-only observations with source provenance and timestamps.
- Predictions are immutable and later settled against official results.
- Training and evaluation use chronological splits only.

See [TRAV_AGENT.md](TRAV_AGENT.md) for the complete research directive.

## Prerequisites

- .NET SDK 10
- A SQL Server connection string in `TRAVMODEL_SQL_CONNECTION`
- Explicit authorization for any live racing-data endpoint

Never commit credentials. Bulk Svensk Travsport collection remains disabled until an authorized endpoint or export is configured.

## Quick start

```powershell
dotnet restore
dotnet test
dotnet run --project src/TravModel.Cli -- doctor
dotnet run --project src/TravModel.Cli -- init-db
dotnet run --project src/TravModel.Cli -- demo-predict
```

The project also supports forward-accumulating enrichment when a bulk historical endpoint is unavailable. Recent-start exports and timestamped facts can be imported repeatedly; overlapping windows are reconciled without pretending the data was known before it was retrieved. See [docs/incremental-collection.md](docs/incremental-collection.md).

See [automation/README.md](automation/README.md) for the scheduled workflow and
[docs/smhi-open-data.md](docs/smhi-open-data.md) for the weather endpoints and field mappings.
