# Swedish Trot Data Research Directive

This directive applies to research and data collection for Swedish harness racing, including races used in V86, V85, V65, V64, V5, V4 and V3.

The objective is to collect reproducible, source-backed race data suitable for analysis, feature engineering, historical evaluation and predictive modelling.

## Core rule

Treat the underlying race as the primary sporting entity.

Do not model V86, V85, V65, V64, V5, V4 or V3 as separate race types. These are betting products that reference ordinary races.

Use this conceptual hierarchy:

```text
Meeting
└── Race
    ├── Starter
    │   ├── Horse
    │   ├── Driver
    │   ├── Trainer
    │   ├── Equipment
    │   └── HistoricalStarts
    ├── Track
    ├── Weather
    ├── Result
    └── BettingMarkets
        ├── V86
        ├── V85
        ├── V65
        ├── V64
        ├── V5
        ├── V4
        └── V3
```

## Source priority

IMPORTANT - ALWAYS check the internet for new and relevant sources. If such are found they should be added to this file.

Use sources in approximately this order.


### MUST prefer official sporting data

1. Svensk Travsport

Primary authority for Swedish sporting facts.

Sites:

* https://www.travsport.se/
* https://sportapp.travsport.se/

Use for:

* Race calendar
* Meetings
* Start lists
* Race conditions
* Horse identity
* Horse history
* Driver identity
* Trainer identity
* Post position
* Distance
* Start method
* Race class/type
* Scratches
* Results
* Kilometer times
* Winning margins
* Track condition
* Shoe information
* Sulky information
* Historical starts
* Official statistics

When Svensk Travsport conflicts with a secondary source, prefer Svensk Travsport unless there is clear evidence that the official value has not yet been updated.

### MUST prefer ATG for current betting-market state

2. ATG

Main site:

* https://www.atg.se/

Use for:

* V86/V85/V65/V64/V5/V4/V3 composition
* Current betting percentages
* Current odds
* Betting turnover
* Pool information
* Scratches as presented to bettors
* Results and payouts
* Betting-market movement
* Equipment information displayed in the betting interface

ATG is the authority for the current betting product and betting-market state, not for historical sporting facts when official Svensk Travsport data exists.

### High-value secondary sources

#### Travfakta

* https://travfakta.se/

Use where available for:

* Historical starts
* Race comments
* Horse statistics
* Driver statistics
* Trainer statistics
* Horse/driver combinations
* Sulky history
* Intermediate race information
* Head-to-head comparisons
* Betting-race statistics

Race comments are particularly valuable because they may describe events that finishing position alone does not capture.

Examples:

* Galloped while advancing
* Trapped behind horses
* Led easily
* Pressed early
* Finished strongly
* Appeared to have energy remaining
* Lost ground because of interference

#### Travmaskinen

* https://travmaskinen.se/

Use for:

* Horse statistics
* Driver statistics
* Trainer statistics
* Driver/trainer combinations
* Recent trainer form
* Historical race statistics
* Track statistics
* Historical odds
* Kilometer-time statistics
* Equipment statistics

#### Travkollen

* https://www.travkollen.se/

Use for:

* Track-specific statistics
* Starting-position statistics
* Start-method statistics
* Distance statistics
* Driver/trainer statistics
* Physical track characteristics

Potential static track features include:

```text
TrackCircumference
HomestretchLength
TrackWidth
TurnRadius
Banking
OpenStretch
AngledStartingGateWing
```

#### Skoinfo

* https://skoinfo.se/

Use for:

* Shoe changes
* Balance changes
* Sulky changes
* Late equipment declarations
* Change timestamps

Prefer capturing both previous and current states.

Example:

```text
ShoesPreviousStart
ShoesToday
ShoesChanged
SulkyPreviousStart
SulkyToday
SulkyChanged
EquipmentChangeTimestamp
MinutesBeforeStart
```

#### Sulkysport

* https://sulkysport.se/

Use as a secondary source or validation source for:

* Start lists
* Horse
* Driver
* Trainer
* Recent form
* Post/distance
* Record
* Earnings
* Shoes
* Sulky
* Race conditions
* Weather
* Track condition

Do not prefer Sulkysport over Svensk Travsport when both contain the same sporting fact.

#### Travet.se

* https://www.travet.se/

Potential use:

* Current betting percentages
* Percentage changes
* Market movers
* Shoe information
* Betting-product start lists

Treat as secondary market information.

#### SverigeTravet

* https://www.sverigetravet.se/

Potential use:

* Start lists
* Odds
* ATG percentages
* Shoe information

Use primarily for validation or fallback.

### Weather source

#### SMHI Open Data

* https://www.smhi.se/data

Use as the preferred weather source for Swedish races.

Potential fields:

```text
Temperature
Precipitation1h
Precipitation6h
WindSpeed
WindDirection
Humidity
Visibility
```

Do not infer track condition solely from weather.

Track condition reported by the racecourse or Svensk Travsport remains a separate variable.

## Required provenance

Every collected value that may change over time MUST retain source provenance where practical.

Prefer recording:

```text
SourceName
SourceUrl
RetrievedAtUtc
ObservedAt
RawValue
NormalizedValue
```

Example:

```json
{
  "field": "BettingPercentage",
  "value": 18.4,
  "sourceName": "ATG",
  "sourceUrl": "...",
  "retrievedAtUtc": "2026-09-12T13:42:17Z"
}
```

Never overwrite a historical observation with a later value when time-series behaviour matters.

Append a new observation instead.

## Separate facts from derived values

MUST distinguish source facts from computed or inferred values.

Example:

```text
OBSERVED
HorseNumber = 4
PostPosition = 4
Distance = 2140
StartMethod = Auto
BettingPercentage = 18.4
ShoesToday = BarefootFront
DERIVED
PostPositionScore = 0.72
MarketMomentum = +0.14
TrainerFormScore = 0.81
DistanceSuitability = High
WinProbability = 0.163
```

Never store a model judgement as if it were supplied by a source.

## Time-sensitive data

The following data SHOULD be treated as time-series data:

* Betting percentage
* Odds
* Scratches
* Shoes
* Balance
* Sulky
* Driver changes
* Trainer changes where applicable
* Track condition
* Weather
* Market ranking

When possible, collect snapshots at:

```text
T-24h
T-6h
T-60m
T-15m
T-5m
Final
```

Do not fabricate missing snapshots.

Derived examples:

```text
BetPctChange24h
BetPctChange60m
OddsChange60m
LateMarketMomentum
LateEquipmentChange
MarketRankChange
```

## Historical start data

For every starter, collect recent historical starts where practical.

A historical start SHOULD contain:

```text
Date
Track
RaceNumber
Distance
StartMethod
PostPosition
Driver
Trainer
FinishPosition
KilometerTime
Odds
Shoes
Sulky
TrackCondition
PrizeMoney
GallopIndicator
RaceComment
```

Prefer raw historical starts over pre-computed “form numbers” whenever raw data is available.

## Driver and trainer data

Where sufficient history exists, derive statistics rather than relying only on lifetime totals.

Useful windows include:

```text
Last14Days
Last30Days
Last90Days
CurrentYear
Previous365Days
```

Potential derived fields:

```text
DriverWinRate30d
DriverPlaceRate30d
TrainerWinRate30d
TrainerPlaceRate30d
DriverTrainerWinRate
HorseDriverWinRate
HorseTrainerWinRate
```

Always record the sample size behind rates.

Example:

```text
TrainerWinRate30d = 0.143
TrainerStarts30d = 42
```

Avoid interpreting very small samples as strong evidence.

## Post-position statistics

When calculating post-position effects, stratify by relevant race context.

At minimum consider:

```text
Track
StartMethod
Distance
FieldSize
PostPosition
```

Do not use a national aggregate post-position statistic when a sufficiently large track-specific sample exists.

## Race comments

Race comments SHOULD be retained as source text where licensing permits.

They may later be transformed into structured features such as:

```text
Galloped
Led
LedEarly
OutsideLeader
TrappedInside
Blocked
Interference
WideTrip
StrongFinish
WeakFinish
EnergyRemaining
EarlyPressure
LateAdvance
```

Keep the original source comment alongside any extracted feature.

## Undocumented APIs and structured endpoints

When a site loads data dynamically, Codex MAY inspect browser/network behaviour or client-side source code to identify structured JSON endpoints.

Preferred order:

1. Official documented API
2. Official export
3. Official JSON endpoint used by the site's own client
4. Structured HTML
5. Rendered HTML parsing
6. Browser automation

Do not assume an undocumented endpoint is stable.

Wrap undocumented endpoints behind a provider abstraction so they can be replaced without affecting the domain model.

Example:

```text
IRaceProvider
IBettingMarketProvider
IWeatherProvider
IEquipmentProvider
IHistoricalResultsProvider
```

## Endpoint discovery rules

When investigating a new site:

1. Open the page normally.
2. Inspect network/XHR/fetch requests.
3. Identify JSON or structured responses.
4. Determine whether IDs are stable.
5. Determine whether pagination exists.
6. Determine whether historical dates can be queried.
7. Determine whether authentication or cookies are required.
8. Determine whether requests are rate limited.
9. Save representative payloads as test fixtures where legally permitted.
10. Implement parsing against the structured response instead of rendered HTML where possible.

Do not bypass authentication, access controls, paywalls or technical restrictions.

## Normalization

Normalize names and identifiers carefully.

Prefer official IDs over string matching.

Examples:

```text
HorseId
DriverId
TrainerId
TrackId
MeetingId
RaceId
```

Do not treat horse name as a permanent unique identifier.

Names may collide or change.

Store both:

```text
ExternalSource
ExternalId
```

Example:

```text
ExternalSource = "SvenskTravsport"
ExternalId = "..."
```

## Data conflicts

When sources disagree:

```text
Official sporting fact:
    Svensk Travsport wins
Current betting state:
    ATG wins
Weather:
    SMHI wins for meteorological observation
Track condition:
    Official race source wins
Equipment:
    Prefer latest timestamped official declaration
```

Never silently replace conflicting values.

Where practical, record both observations and mark which source is authoritative.

## Missing data

Never invent missing values.

Use explicit states such as:

```text
null
Unknown
NotReported
NotApplicable
```

Do not convert missing data to zero unless zero is semantically correct.

## Data quality

For every provider, consider validating:

```text
Freshness
Completeness
Duplicate records
Unexpected schema changes
Invalid identifiers
Missing starters
Scratches
Field-size mismatch
Race-number mismatch
Date mismatch
Track mismatch
```

A failed validation SHOULD prevent unreliable data from being silently treated as valid.

## Suggested provider architecture

Prefer independent adapters.

Example:

```text
SvenskTravsportProvider
AtgProvider
TravfaktaProvider
TravmaskinenProvider
TravkollenProvider
SkoinfoProvider
SmhiProvider
```

Normalize them into common domain records.

Example:

```text
External providers
        │
        ▼
Provider-specific DTOs
        │
        ▼
Normalization
        │
        ▼
Canonical Trav domain model
        │
        ├── Raw observations
        ├── Historical snapshots
        └── Derived features
```

Do not let provider-specific JSON structures leak into the main domain model.

## Caching

Cache historical data aggressively.

Examples:

```text
Horse history
Driver history
Trainer history
Track metadata
Past results
Past weather
```

Refresh frequently changing data more often.

Examples:

```text
Current odds
Betting percentage
Scratches
Equipment
Track condition
Weather
```

## Reproducibility

A historical analysis MUST be reproducible using only information that existed at the analysis timestamp.

Do not accidentally use future information.

For a race at time T, pre-race feature generation MUST NOT use:

```text
Result of the race at T
Post-race comments
Final information published after T
Future starts
Future trainer statistics
Future driver statistics
```

All data queries used for historical modelling MUST support point-in-time correctness.

## Suggested source-selection flow

For each race:

1. Resolve official meeting and race using Svensk Travsport.
2. Resolve all starters and official race conditions.
3. Retrieve historical starts.
4. Retrieve driver/trainer data.
5. Resolve betting-product membership through ATG.
6. Capture current ATG percentages and odds.
7. Capture scratches and participant changes.
8. Capture shoe/balance/sulky information.
9. Capture track metadata.
10. Capture SMHI weather.
11. Add Travfakta/Travmaskinen/Travkollen enrichment.
12. Store raw observations.
13. Normalize into canonical entities.
14. Compute derived features separately.

## Minimum source set

If only a small number of integrations can be implemented initially, prioritize:

1. Svensk Travsport
2. ATG
3. SMHI
4. Skoinfo
5. Travfakta
6. Travmaskinen or Travkollen

The first two are the critical pair:

```text
Svensk Travsport = sporting truth
ATG              = betting-market truth
```

## Implementation principle

Do not optimize the data model around a particular betting product.

Optimize it around reusable race, horse, driver, trainer, track and observation data.

The same normalized dataset should be usable for:

```text
V86
V85
V65
V64
V5
V4
V3
Single-race analysis
Historical backtesting
Model training
Market analysis
```

All source adapters should remain replaceable without changing the core racing domain.
