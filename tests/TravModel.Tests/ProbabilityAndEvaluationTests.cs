using TravModel.Application;
using TravModel.Domain;
using TravModel.ML;

namespace TravModel.Tests;

public sealed class ProbabilityAndEvaluationTests
{
    [Fact]
    public void Normalize_ProducesRaceDistributionAndZeroForScratch()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var scratched = Guid.NewGuid();
        var result = RaceProbabilityNormalizer.Normalize([(first, false, 2d), (second, false, 1d), (scratched, true, 100d)]);

        Assert.Equal(1d, result.Sum(x => x.Probability), 12);
        Assert.Equal(first, result.Single(x => x.Rank == 1).StarterId);
        Assert.Equal(0d, result.Single(x => x.StarterId == scratched).Probability);
        Assert.Equal(0, result.Single(x => x.StarterId == scratched).Rank);
    }

    [Fact]
    public void Metrics_RewardProbabilityAssignedToWinner()
    {
        var winner = Guid.NewGuid();
        var loser = Guid.NewGuid();
        var strong = new SettledRacePrediction(Guid.NewGuid(), winner,
            [new StarterProbability(winner, 0.8, 1), new StarterProbability(loser, 0.2, 2)]);
        var weak = strong with
        {
            Probabilities = [new StarterProbability(winner, 0.2, 2), new StarterProbability(loser, 0.8, 1)]
        };

        var strongMetrics = PredictionMetrics.Evaluate(Guid.NewGuid(), [strong], DateTimeOffset.MinValue, DateTimeOffset.MaxValue);
        var weakMetrics = PredictionMetrics.Evaluate(Guid.NewGuid(), [weak], DateTimeOffset.MinValue, DateTimeOffset.MaxValue);

        Assert.True(strongMetrics.LogLoss < weakMetrics.LogLoss);
        Assert.True(strongMetrics.BrierScore < weakMetrics.BrierScore);
        Assert.Equal(1, strongMetrics.TopOneAccuracy);
    }

    [Fact]
    public void PromotionPolicy_EnforcesAllGuardrails()
    {
        var incumbent = Evaluation(1.0, 0.5, 0.04, 1.0);
        var challenger = Evaluation(0.99, 0.5, 0.04, 1.0);
        challenger.LogLossImprovementLower95 = 0.001;

        Assert.True(ModelPromotionPolicy.Evaluate(incumbent, challenger).Promote);
        challenger.CalibrationError = 0.046;
        Assert.False(ModelPromotionPolicy.Evaluate(incumbent, challenger).Promote);
    }

    [Fact]
    public void MarketBaseline_NormalizesImpliedProbabilitiesWithinRace()
    {
        var favourite = TestData.Starter(TestData.Race(DateTimeOffset.UtcNow.AddHours(1)), 1).Id;
        var outsider = Guid.NewGuid();
        var features = new RaceFeatureSet(Guid.NewGuid(), DateTimeOffset.UtcNow, "market",
        [
            Vector(favourite, 60, 1.5f),
            Vector(outsider, 20, 6f)
        ]);

        var result = new MarketImpliedProbabilityModel(Guid.Empty).Predict(features);

        Assert.Equal(1d, result.Sum(x => x.Probability), 12);
        Assert.Equal(favourite, result.Single(x => x.Rank == 1).StarterId);
    }

    private static StarterFeatureVector Vector(Guid id, float percentage, float odds) =>
        new(id, false, 1, 1, 2140, 2, 0, float.NaN, float.NaN, float.NaN, float.NaN,
            float.NaN, 0, float.NaN, 0, percentage, odds, float.NaN, float.NaN);

    private static ModelEvaluation Evaluation(double logLoss, double brier, double calibration, double coverage) => new()
    {
        RaceCount = 1_000,
        LogLoss = logLoss,
        BrierScore = brier,
        CalibrationError = calibration,
        Coverage = coverage
    };
}
