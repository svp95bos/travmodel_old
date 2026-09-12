using TravModel.Domain;

namespace TravModel.Application;

public static class RaceProbabilityNormalizer
{
    public static IReadOnlyList<StarterProbability> Normalize(
        IReadOnlyCollection<(Guid StarterId, bool IsScratched, double Score)> scores,
        double temperature = 1)
    {
        ArgumentNullException.ThrowIfNull(scores);
        if (temperature <= 0 || double.IsNaN(temperature))
        {
            throw new ArgumentOutOfRangeException(nameof(temperature));
        }

        var active = scores.Where(x => !x.IsScratched).ToArray();
        if (active.Length == 0)
        {
            throw new InvalidOperationException("A race must have at least one active starter.");
        }

        var max = active.Max(x => x.Score / temperature);
        var weights = active.Select(x => (x.StarterId, Weight: Math.Exp((x.Score / temperature) - max))).ToArray();
        var sum = weights.Sum(x => x.Weight);
        var ranked = weights
            .Select(x => (x.StarterId, Probability: x.Weight / sum))
            .OrderByDescending(x => x.Probability)
            .ThenBy(x => x.StarterId)
            .Select((x, index) => new StarterProbability(x.StarterId, x.Probability, index + 1))
            .ToList();

        ranked.AddRange(scores.Where(x => x.IsScratched).Select(x => new StarterProbability(x.StarterId, 0, 0)));
        return ranked.OrderBy(x => x.Rank == 0 ? int.MaxValue : x.Rank).ToArray();
    }
}

public sealed class PredictionService(ITravRepository repository, IWinProbabilityModel model, IClock clock)
{
    public async Task<PredictionRun> PredictAsync(Guid raceId, DateTimeOffset cutoffUtc, CancellationToken cancellationToken)
    {
        var input = await repository.GetFeatureInputAsync(raceId, cutoffUtc, cancellationToken)
            .ConfigureAwait(false) ?? throw new InvalidOperationException($"Race {raceId} was not found.");
        var features = PointInTimeFeatureBuilder.Build(input, cutoffUtc);
        var probabilities = model.Predict(features);
        var run = new PredictionRun
        {
            RaceId = raceId,
            ModelVersionId = model.ModelVersionId,
            FeatureCutoffUtc = cutoffUtc,
            CreatedAtUtc = clock.UtcNow,
            DatasetFingerprint = features.DatasetFingerprint
        };
        run.Predictions.AddRange(probabilities.Select(x => new StarterPrediction
        {
            PredictionRunId = run.Id,
            StarterId = x.StarterId,
            WinProbability = x.Probability,
            Rank = x.Rank
        }));
        await repository.SavePredictionRunAsync(run, cancellationToken).ConfigureAwait(false);
        return run;
    }
}

public sealed record PromotionDecision(bool Promote, IReadOnlyList<string> Reasons);

public static class ModelPromotionPolicy
{
    public const int MinimumRaces = 1_000;

    public static PromotionDecision Evaluate(ModelEvaluation incumbent, ModelEvaluation challenger)
    {
        var reasons = new List<string>();
        if (challenger.RaceCount < MinimumRaces) reasons.Add($"Requires at least {MinimumRaces} settled races.");
        var improvement = (incumbent.LogLoss - challenger.LogLoss) / incumbent.LogLoss;
        if (improvement < 0.005) reasons.Add("Log loss improvement is below 0.5%.");
        if (challenger.LogLossImprovementLower95 is null or <= 0) reasons.Add("The 95% lower confidence bound does not exclude no improvement.");
        if (challenger.BrierScore > incumbent.BrierScore * 1.005) reasons.Add("Brier score regressed by more than 0.5%.");
        if (challenger.CalibrationError > incumbent.CalibrationError + 0.005) reasons.Add("Calibration error regressed by more than 0.005.");
        if (challenger.Coverage < incumbent.Coverage) reasons.Add("Prediction coverage regressed.");
        return new PromotionDecision(reasons.Count == 0, reasons);
    }
}
