# Point-in-time data contract

## Truth ownership

- Svensk Travsport owns official sporting facts and results.
- ATG owns current betting-product and market state.
- SMHI owns meteorological observations.
- Official racecourse/Travsport reporting owns track condition.
- The most recent timestamped official declaration owns equipment state.

Conflicting observations are retained. Authority selects the value used by a feature; it does not erase alternatives.

## Temporal rules

A feature generated for cutoff `C` may use an observation only when both conditions hold:

```text
RetrievedAtUtc <= C
ObservedAtUtc is null OR ObservedAtUtc <= C
```

Forecast observations also carry `ValidAtUtc`, the weather time the forecast describes. Their
`ObservedAtUtc` is the SMHI model reference time, so a forecast is admitted only if that model run
existed by cutoff `C`. Forecast fields are named separately from measured MetObs fields.

Historical starts must have `StartTimeUtc < C`. Results, comments, future starts, future rolling statistics, and late declarations are forbidden.

The T-15 operational task polls every five minutes and selects races 15–20 minutes from scheduled start. The precise feature cutoff is stored; missing snapshots stay missing.

## Storage layers

1. Raw: content-addressed provider payloads under `data/raw`, excluded from Git.
2. Observations: append-only SQL rows retaining raw and normalized values plus provenance.
3. Canonical: race-centred normalized SQL entities keyed by official external IDs.
4. Curated: immutable date partitions and checksummed manifests under `data/curated`, committed through daily PRs.
5. Derived: feature datasets and model artifacts, reproducible from a manifest and excluded from Git when bulky.

## Prediction contract

Every active starter receives a probability; scratched starters receive probability zero and rank zero. Active probabilities must sum to one. A prediction run records race, model version, creation time, feature cutoff, dataset fingerprint, and individual ranks/probabilities. Once created, it is settled but never rewritten.

## Promotion contract

A challenger requires at least 1,000 evaluation races, at least 0.5% lower race-level log loss, a positive bootstrap 95% lower bound, no more than 0.5% Brier regression, no more than 0.005 calibration regression, and no coverage regression.
