using Microsoft.ML;
using TravModel.Application;
using TravModel.Domain;

namespace TravModel.ML;

public sealed record LabeledRaceFeatureSet(RaceFeatureSet Features, Guid WinningStarterId);

public sealed record TrainingResult(
    Guid ModelVersionId,
    ModelKind Kind,
    int RaceCount,
    int StarterCount,
    string ArtifactPath);

public sealed class MlNetWinModelTrainer
{
    private readonly MLContext context;

    public MlNetWinModelTrainer(int seed = 1854)
    {
        context = new MLContext(seed);
    }

    public TrainingResult Train(
        IReadOnlyCollection<LabeledRaceFeatureSet> races,
        ModelKind kind,
        Guid modelVersionId,
        string artifactPath)
    {
        ArgumentNullException.ThrowIfNull(races);
        if (races.Count == 0) throw new ArgumentException("Training requires at least one race.", nameof(races));
        var rows = races.SelectMany(race => race.Features.Starters
            .Where(x => !x.IsScratched)
            .Select(x => TrainingRow.From(x, x.StarterId == race.WinningStarterId, kind))).ToArray();
        if (!rows.Any(x => x.Label) || rows.All(x => x.Label))
        {
            throw new InvalidOperationException("Training data must contain winner and non-winner rows.");
        }

        var data = context.Data.LoadFromEnumerable(rows);
        var featureColumns = TrainingRow.FeatureColumns(kind);
        var pipeline = context.Transforms.Concatenate("Features", featureColumns)
            .Append(context.BinaryClassification.Trainers.LightGbm(
                labelColumnName: nameof(TrainingRow.Label),
                featureColumnName: "Features"));
        var model = pipeline.Fit(data);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(artifactPath))!);
        context.Model.Save(model, data.Schema, artifactPath);
        return new TrainingResult(modelVersionId, kind, races.Count, rows.Length, artifactPath);
    }
}

public sealed class MlNetWinProbabilityModel : IWinProbabilityModel
{
    private readonly MLContext context = new(seed: 1854);
    private readonly ITransformer transformer;
    private readonly double temperature;

    public MlNetWinProbabilityModel(Guid modelVersionId, ModelKind kind, string artifactPath, double temperature = 1)
    {
        ModelVersionId = modelVersionId;
        Kind = kind;
        this.temperature = temperature;
        transformer = context.Model.Load(artifactPath, out _);
    }

    public Guid ModelVersionId { get; }
    public ModelKind Kind { get; }

    public IReadOnlyList<StarterProbability> Predict(RaceFeatureSet race)
    {
        ArgumentNullException.ThrowIfNull(race);
        var rows = race.Starters.Select(x => TrainingRow.From(x, false, Kind)).ToArray();
        var scored = transformer.Transform(context.Data.LoadFromEnumerable(rows));
        var scores = context.Data.CreateEnumerable<ScoreRow>(scored, reuseRowObject: false).ToArray();
        var raw = race.Starters.Zip(scores, (starter, score) => (starter.StarterId, starter.IsScratched, (double)score.Score)).ToArray();
        return RaceProbabilityNormalizer.Normalize(raw, temperature);
    }
}

public sealed class UniformWinProbabilityModel(Guid modelVersionId, ModelKind kind = ModelKind.SportingOnly) : IWinProbabilityModel
{
    public Guid ModelVersionId { get; } = modelVersionId;
    public ModelKind Kind { get; } = kind;

    public IReadOnlyList<StarterProbability> Predict(RaceFeatureSet race) =>
        RaceProbabilityNormalizer.Normalize(race.Starters.Select(x => (x.StarterId, x.IsScratched, 0d)).ToArray());
}

public sealed class MarketImpliedProbabilityModel(Guid modelVersionId) : IWinProbabilityModel
{
    public Guid ModelVersionId { get; } = modelVersionId;
    public ModelKind Kind => ModelKind.MarketAware;

    public IReadOnlyList<StarterProbability> Predict(RaceFeatureSet race)
    {
        var scores = race.Starters.Select(x =>
        {
            var weight = !float.IsNaN(x.BettingPercentage) && x.BettingPercentage > 0
                ? x.BettingPercentage / 100d
                : !float.IsNaN(x.MarketOdds) && x.MarketOdds > 0
                    ? 1d / x.MarketOdds
                    : 1d;
            return (x.StarterId, x.IsScratched, Math.Log(weight));
        }).ToArray();
        return RaceProbabilityNormalizer.Normalize(scores);
    }
}

internal sealed class TrainingRow
{
    public bool Label { get; init; }
    public float HorseNumber { get; init; }
    public float PostPosition { get; init; }
    public float DistanceMetres { get; init; }
    public float FieldSize { get; init; }
    public float RecentStarts { get; init; }
    public float RecentWinRate { get; init; }
    public float RecentPlaceRate { get; init; }
    public float RecentGallopRate { get; init; }
    public float MeanKilometerTimeSeconds { get; init; }
    public float DriverWinRate30Days { get; init; }
    public float DriverStarts30Days { get; init; }
    public float TrainerWinRate30Days { get; init; }
    public float TrainerStarts30Days { get; init; }
    public float BettingPercentage { get; init; }
    public float MarketOdds { get; init; }
    public float TemperatureCelsius { get; init; }
    public float WindSpeedMetresPerSecond { get; init; }

    public static TrainingRow From(StarterFeatureVector x, bool label, ModelKind kind) => new()
    {
        Label = label,
        HorseNumber = x.HorseNumber,
        PostPosition = x.PostPosition,
        DistanceMetres = x.DistanceMetres,
        FieldSize = x.FieldSize,
        RecentStarts = x.RecentStarts,
        RecentWinRate = x.RecentWinRate,
        RecentPlaceRate = x.RecentPlaceRate,
        RecentGallopRate = x.RecentGallopRate,
        MeanKilometerTimeSeconds = x.MeanKilometerTimeSeconds,
        DriverWinRate30Days = x.DriverWinRate30Days,
        DriverStarts30Days = x.DriverStarts30Days,
        TrainerWinRate30Days = x.TrainerWinRate30Days,
        TrainerStarts30Days = x.TrainerStarts30Days,
        BettingPercentage = kind == ModelKind.MarketAware ? x.BettingPercentage : 0,
        MarketOdds = kind == ModelKind.MarketAware ? x.MarketOdds : 0,
        TemperatureCelsius = x.TemperatureCelsius,
        WindSpeedMetresPerSecond = x.WindSpeedMetresPerSecond
    };

    public static string[] FeatureColumns(ModelKind kind)
    {
        var columns = new List<string>
        {
            nameof(HorseNumber), nameof(PostPosition), nameof(DistanceMetres), nameof(FieldSize),
            nameof(RecentStarts), nameof(RecentWinRate), nameof(RecentPlaceRate), nameof(RecentGallopRate),
            nameof(MeanKilometerTimeSeconds), nameof(DriverWinRate30Days), nameof(DriverStarts30Days),
            nameof(TrainerWinRate30Days), nameof(TrainerStarts30Days), nameof(TemperatureCelsius),
            nameof(WindSpeedMetresPerSecond)
        };
        if (kind == ModelKind.MarketAware)
        {
            columns.Add(nameof(BettingPercentage));
            columns.Add(nameof(MarketOdds));
        }

        return [.. columns];
    }
}

internal sealed class ScoreRow
{
    public float Score { get; init; }
}
