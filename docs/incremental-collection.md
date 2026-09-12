# Incremental collection without a historical bulk endpoint

The system grows its own point-in-time history. It does not disguise data discovered today as data known in the past.

## Source activation

Record a qualification decision independently for each capability:

```powershell
dotnet run --project src/TravModel.Cli -- qualify-source `
  --source ATG `
  --capability equipment `
  --base-uri https://www.atg.se/ `
  --status Approved `
  --terms-url https://example.test/reviewed-terms `
  --robots-url https://www.atg.se/robots.txt

dotnet run --project src/TravModel.Cli -- source-status
```

Use `Approved` only after checking terms, robots directives, stable identifiers, request limits and the intended collection method. `Pending`, `Disallowed` and `Disabled` capabilities are not called by the incremental collection coordinator.

## Import contract

Until a qualified source-specific adapter exists, permitted exports or captured structured responses can enter through `ingest-enrichment`. The whole input is retained in content-addressed raw storage.

```json
{
  "kind": "recent-starts",
  "sourceName": "ATG",
  "sourceUrl": "https://example.test/source-page",
  "retrievedAtUtc": "2026-09-12T12:00:00Z",
  "observedAtUtc": "2026-09-12T11:59:00Z",
  "records": [
    {
      "horseExternalId": "horse-1",
      "externalRaceId": "race-1",
      "startTimeUtc": "2026-08-20T18:00:00Z",
      "trackName": "Solvalla",
      "raceNumber": 5,
      "distanceMetres": 2140,
      "startMethod": "Auto",
      "postPosition": 4,
      "finishPosition": 2,
      "kilometerTimeSeconds": 72.4,
      "odds": 3.2,
      "shoes": "BarefootFront",
      "sulky": "American",
      "galloped": false
    }
  ]
}
```

Timestamped entity or starter facts use `kind: "facts"` and records matching `ProviderFact`: `entityType`, provider `externalId`, `field`, raw and normalized values, state, observation/validity times and authority.

```powershell
dotnet run --project src/TravModel.Cli -- ingest-enrichment --file data/inbox/recent-starts.json
```

Unknown external identities are not name-matched. They create pending identity reviews and their facts remain unattached until resolved.

After verifying a match against stable identifiers, link it explicitly:

```powershell
dotnet run --project src/TravModel.Cli -- link-identity --entity-type Horse --entity-id <canonical-guid> --source Travmaskinen --external-id <provider-id>
```

## Daily workflow

1. Synchronize all Swedish meetings for today through seven days ahead.
2. Run each approved recent-form/profile adapter for newly seen or stale entrants.
3. Capture changing starter facts at T-24h, T-6h, T-60m, T-15m and T-5m.
4. Synchronize official results nightly with a rolling seven-day repair window. Each result adds a reusable historical start.
5. Inspect collection health:

```powershell
dotnet run --project src/TravModel.Cli -- data-health --from 2026-09-01 --to 2026-09-12
dotnet run --project src/TravModel.Cli -- build-shadow-dataset --through 2026-09-12T23:59:59Z
```

The shadow dataset reports history depth and equipment changes but does not alter production model inputs.

## Failure behavior

Provider failures are isolated by capability and stored in `ProviderCheckpoint`. Other providers continue, missing fields remain missing, and `data-health` returns a degraded exit code while active alerts exist. Schema-invalid inputs must be rejected before canonical projection.
