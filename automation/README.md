# Scheduled operation

The scheduled agent supervises deterministic CLI commands. Configure the tasks in ChatGPT desktop against this repository and use `Europe/Stockholm` as the scheduling timezone. The machine and desktop app must remain running for tasks that access this local project.

## Required environment

```text
TRAVMODEL_SQL_CONNECTION
TRAVMODEL_SVENSKTRAVSPORT_ENABLED=true
TRAVMODEL_SVENSKTRAVSPORT_AUTHORIZED=true
TRAVMODEL_SVENSKTRAVSPORT_BASE_URI=https://authorized-gateway.example/
TRAVMODEL_ATG_ENABLED=true
TRAVMODEL_ATG_AUTHORIZED=true
TRAVMODEL_ATG_BASE_URI=https://authorized-gateway.example/
TRAVMODEL_SMHI_ENABLED=true
```

The sporting and betting gateway URIs must expose the canonical JSON contracts defined in `TravModel.Application`. Set `AUTHORIZED=true` only when collection is permitted. SMHI uses the public official SNOW1gv1 forecast and MetObs observation endpoints directly and requires each track to have latitude and longitude.

## Five-minute collection task

Schedule every five minutes in the local project:

```text
Read AGENTS.md and TRAV_AGENT.md. Run `dotnet run --project src/TravModel.Cli -- collect-due --as-of <current UTC instant>`. Do not browse around an authorization failure or disabled provider. Report validation, schema, freshness, or prediction failures. If no race is 15–20 minutes from starting, report a concise no-op.
```

Polling the 15–20 minute window produces one observation close to T-15 while retaining the exact feature cutoff.

## Daily result and export task

Schedule at 02:00 Europe/Stockholm in an isolated worktree:

```text
Read AGENTS.md and TRAV_AGENT.md. Sync yesterday's official results, settle immutable predictions, and export yesterday's curated partition. Verify the manifest hashes and run tests. Create or update a branch named `automation/data-YYYY-MM-DD` and open a pull request. Never push directly to main. Do not include raw payloads, credentials, model binaries, SQL files, or local database files.
```

Equivalent commands:

```powershell
dotnet run --project src/TravModel.Cli -- sync-results --from <yesterday> --to <today>
dotnet run --project src/TravModel.Cli -- settle --through <current UTC instant>
dotnet run --project src/TravModel.Cli -- export-curated --date <yesterday>
dotnet test
```

## Daily calendar repair task

Schedule at 05:00 Europe/Stockholm:

```text
Read AGENTS.md and TRAV_AGENT.md. Sync the official calendar for today through seven days ahead. Retry only transient failures. Report missing meetings, invalid identifiers, race-number/date/track mismatches, and provider schema changes. Never fabricate gaps.
```

## Weekly training task

Schedule Sunday at 03:00 Europe/Stockholm:

```text
Read AGENTS.md and TRAV_AGENT.md. For SportingOnly and MarketAware, run the challenger training and evaluation command through the last fully settled UTC day. A rejected challenger is an expected result, not a reason to weaken the gates. Report the incumbent/challenger metrics and every failed gate.
```

## Operational safeguards

- Start by manually testing each prompt and review the first fourteen days of runs.
- Scheduled tasks must use workspace-write with narrowly approved network and Git commands.
- Raw artifacts remain under `data/raw` and are content-addressed but Git-ignored.
- A data PR contains only `data/curated/race_date=YYYY-MM-DD` partitions and manifests.
- Archive obsolete scheduled-task worktrees regularly.
