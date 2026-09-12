using TravModel.Application;
using TravModel.Domain;
using TravModel.ML;

namespace TravModel.Tests;

public sealed class MlNetWinModelTests
{
    [Fact]
    public void TrainAndLoad_ProducesNormalizedRaceProbabilities()
    {
        var races = Enumerable.Range(0, 40).Select(index => LabeledRace(index)).ToArray();
        var directory = Path.Combine(Path.GetTempPath(), $"travmodel-ml-tests-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "model.zip");
        try
        {
            var id = Guid.NewGuid();
            var result = new MlNetWinModelTrainer().Train(races, ModelKind.SportingOnly, id, path);
            var model = new MlNetWinProbabilityModel(id, ModelKind.SportingOnly, path);
            var probabilities = model.Predict(races[0].Features);

            Assert.Equal(40, result.RaceCount);
            Assert.Equal(1d, probabilities.Sum(x => x.Probability), 12);
            Assert.All(probabilities, x => Assert.InRange(x.Probability, 0, 1));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    private static LabeledRaceFeatureSet LabeledRace(int index)
    {
        var strong = Vector(Guid.NewGuid(), 1, 0.5f + (index % 3) * 0.01f);
        var weak = Vector(Guid.NewGuid(), 2, 0.05f);
        return new LabeledRaceFeatureSet(
            new RaceFeatureSet(Guid.NewGuid(), DateTimeOffset.UtcNow.AddDays(-index), $"race-{index}", [strong, weak]),
            strong.StarterId);
    }

    private static StarterFeatureVector Vector(Guid id, int number, float winRate) =>
        new(id, false, number, number, 2140, 2, 8, winRate, winRate, 0.05f, 75, 0.15f, 50, 0.14f, 45, 0, 0, 10, 3);
}
