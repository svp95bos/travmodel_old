namespace TravModel.Domain;

public enum StartMethod
{
    Unknown,
    Auto,
    Volt,
    Monte
}

public enum ObservationState
{
    Observed,
    Unknown,
    NotReported,
    NotApplicable
}

public enum ModelKind
{
    SportingOnly,
    MarketAware
}

public enum ModelStatus
{
    Challenger,
    Active,
    Rejected,
    Retired
}

public sealed class Track
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public required string ExternalSource { get; set; }
    public required string ExternalId { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public int? CircumferenceMetres { get; set; }
    public int? HomestretchLengthMetres { get; set; }
    public decimal? WidthMetres { get; set; }
    public bool? HasOpenStretch { get; set; }
}

public sealed class Meeting
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string ExternalSource { get; set; }
    public required string ExternalId { get; set; }
    public Guid TrackId { get; set; }
    public Track? Track { get; set; }
    public DateOnly MeetingDate { get; set; }
    public List<Race> Races { get; } = [];
}

public sealed class Race
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string ExternalSource { get; set; }
    public required string ExternalId { get; set; }
    public Guid MeetingId { get; set; }
    public Meeting? Meeting { get; set; }
    public int RaceNumber { get; set; }
    public DateTimeOffset ScheduledStartUtc { get; set; }
    public int DistanceMetres { get; set; }
    public StartMethod StartMethod { get; set; }
    public required string RaceType { get; set; }
    public required string ConditionsText { get; set; }
    public List<Starter> Starters { get; } = [];
    public List<BettingMarketMembership> BettingMarkets { get; } = [];
}

public sealed class Horse
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string ExternalSource { get; set; }
    public required string ExternalId { get; set; }
    public required string Name { get; set; }
    public int? BirthYear { get; set; }
    public required string Sex { get; set; }
}

public sealed class Person
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string ExternalSource { get; set; }
    public required string ExternalId { get; set; }
    public required string Name { get; set; }
}

public sealed class Starter
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RaceId { get; set; }
    public Race? Race { get; set; }
    public Guid HorseId { get; set; }
    public Horse? Horse { get; set; }
    public Guid DriverId { get; set; }
    public Person? Driver { get; set; }
    public Guid TrainerId { get; set; }
    public Person? Trainer { get; set; }
    public int HorseNumber { get; set; }
    public int PostPosition { get; set; }
    public int DistanceMetres { get; set; }
    public bool IsScratched { get; set; }
}

public sealed class RaceResult
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RaceId { get; set; }
    public Race? Race { get; set; }
    public Guid StarterId { get; set; }
    public Starter? Starter { get; set; }
    public int? FinishPosition { get; set; }
    public required string FinishCode { get; set; }
    public decimal? KilometerTimeSeconds { get; set; }
    public decimal? WinningMarginMetres { get; set; }
    public decimal? PrizeMoneySek { get; set; }
    public bool Galloped { get; set; }
    public string? RaceComment { get; set; }
    public required string SourceName { get; set; }
    public required string SourceUrl { get; set; }
    public DateTimeOffset RetrievedAtUtc { get; set; }
    public DateTimeOffset? PublishedAtUtc { get; set; }
}

public sealed class HistoricalStart
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid HorseId { get; set; }
    public Horse? Horse { get; set; }
    public Guid? DriverId { get; set; }
    public Person? Driver { get; set; }
    public Guid? TrainerId { get; set; }
    public Person? Trainer { get; set; }
    public required string ExternalRaceId { get; set; }
    public DateTimeOffset StartTimeUtc { get; set; }
    public required string TrackName { get; set; }
    public int RaceNumber { get; set; }
    public int DistanceMetres { get; set; }
    public StartMethod StartMethod { get; set; }
    public int PostPosition { get; set; }
    public int? FinishPosition { get; set; }
    public decimal? KilometerTimeSeconds { get; set; }
    public decimal? Odds { get; set; }
    public string? Shoes { get; set; }
    public string? Sulky { get; set; }
    public string? TrackCondition { get; set; }
    public decimal? PrizeMoneySek { get; set; }
    public bool Galloped { get; set; }
    public string? RaceComment { get; set; }
}

public sealed class BettingMarketMembership
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RaceId { get; set; }
    public Race? Race { get; set; }
    public required string Product { get; set; }
    public required string ProductId { get; set; }
    public int LegNumber { get; set; }
}

public sealed class Observation
{
    public long Id { get; set; }
    public required string EntityType { get; set; }
    public required string EntityId { get; set; }
    public required string Field { get; set; }
    public required string SourceName { get; set; }
    public required string SourceUrl { get; set; }
    public DateTimeOffset RetrievedAtUtc { get; set; }
    public DateTimeOffset? ObservedAtUtc { get; set; }
    public DateTimeOffset? ValidAtUtc { get; set; }
    public required string RawValue { get; set; }
    public string? NormalizedValue { get; set; }
    public ObservationState State { get; set; }
    public bool IsAuthoritative { get; set; }
    public required string ContentHash { get; set; }
}

public sealed class IngestionRun
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Provider { get; set; }
    public DateTimeOffset StartedAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public required string Status { get; set; }
    public int RecordsRead { get; set; }
    public int RecordsWritten { get; set; }
    public string? Error { get; set; }
}

public sealed class RawArtifactManifest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid IngestionRunId { get; set; }
    public IngestionRun? IngestionRun { get; set; }
    public required string Provider { get; set; }
    public required string SourceUrl { get; set; }
    public required string RelativePath { get; set; }
    public required string Sha256 { get; set; }
    public DateTimeOffset RetrievedAtUtc { get; set; }
    public long SizeBytes { get; set; }
}

public sealed class PredictionRun
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RaceId { get; set; }
    public Race? Race { get; set; }
    public Guid ModelVersionId { get; set; }
    public ModelVersion? ModelVersion { get; set; }
    public DateTimeOffset FeatureCutoffUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public required string DatasetFingerprint { get; set; }
    public List<StarterPrediction> Predictions { get; } = [];
}

public sealed class StarterPrediction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PredictionRunId { get; set; }
    public PredictionRun? PredictionRun { get; set; }
    public Guid StarterId { get; set; }
    public Starter? Starter { get; set; }
    public double WinProbability { get; set; }
    public int Rank { get; set; }
    public decimal? MarketOdds { get; set; }
    public bool? Won { get; set; }
    public DateTimeOffset? SettledAtUtc { get; set; }
}

public sealed class ModelVersion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Version { get; set; }
    public ModelKind Kind { get; set; }
    public ModelStatus Status { get; set; }
    public DateTimeOffset TrainedThroughUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public required string TrainingDatasetFingerprint { get; set; }
    public required string ArtifactPath { get; set; }
}

public sealed class ModelEvaluation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ModelVersionId { get; set; }
    public ModelVersion? ModelVersion { get; set; }
    public int RaceCount { get; set; }
    public double LogLoss { get; set; }
    public double BrierScore { get; set; }
    public double CalibrationError { get; set; }
    public double TopOneAccuracy { get; set; }
    public double Coverage { get; set; }
    public double? FlatStakeRoi { get; set; }
    public double? LogLossImprovementLower95 { get; set; }
    public DateTimeOffset WindowStartUtc { get; set; }
    public DateTimeOffset WindowEndUtc { get; set; }
}
