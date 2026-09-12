using TravModel.Domain;

namespace TravModel.Application;

public sealed record SettledRacePrediction(
    Guid RaceId,
    Guid WinningStarterId,
    IReadOnlyList<StarterProbability> Probabilities);

public static class PredictionMetrics
{
    private const double Epsilon = 1e-15;

    public static ModelEvaluation Evaluate(
        Guid modelVersionId,
        IReadOnlyCollection<SettledRacePrediction> races,
        DateTimeOffset windowStartUtc,
        DateTimeOffset windowEndUtc)
    {
        ArgumentNullException.ThrowIfNull(races);
        if (races.Count == 0) throw new ArgumentException("At least one settled race is required.", nameof(races));

        var logLoss = 0d;
        var brier = 0d;
        var topOneWins = 0;
        var calibrationRows = new List<(double Probability, bool Won)>();
        foreach (var race in races)
        {
            var active = race.Probabilities.Where(x => x.Rank > 0).ToArray();
            var winner = active.SingleOrDefault(x => x.StarterId == race.WinningStarterId)
                ?? throw new InvalidOperationException($"Winner missing from race {race.RaceId} predictions.");
            logLoss -= Math.Log(Math.Clamp(winner.Probability, Epsilon, 1 - Epsilon));
            brier += active.Sum(x => Math.Pow(x.Probability - (x.StarterId == race.WinningStarterId ? 1 : 0), 2));
            if (active.MinBy(x => x.Rank)!.StarterId == race.WinningStarterId) topOneWins++;
            calibrationRows.AddRange(active.Select(x => (x.Probability, x.StarterId == race.WinningStarterId)));
        }

        return new ModelEvaluation
        {
            ModelVersionId = modelVersionId,
            RaceCount = races.Count,
            LogLoss = logLoss / races.Count,
            BrierScore = brier / races.Count,
            CalibrationError = ExpectedCalibrationError(calibrationRows, 10),
            TopOneAccuracy = (double)topOneWins / races.Count,
            Coverage = 1,
            WindowStartUtc = windowStartUtc,
            WindowEndUtc = windowEndUtc
        };
    }

    public static double BootstrapLogLossImprovementLower95(
        IReadOnlyList<SettledRacePrediction> incumbent,
        IReadOnlyList<SettledRacePrediction> challenger,
        int iterations = 1_000,
        int seed = 1854)
    {
        if (incumbent.Count != challenger.Count || incumbent.Count == 0)
            throw new ArgumentException("Models must cover the same non-empty race set.");
        var incumbentByRace = incumbent.ToDictionary(x => x.RaceId);
        var differences = challenger.Select(race =>
        {
            var incumbentRace = incumbentByRace[race.RaceId];
            return WinnerLogLoss(incumbentRace) - WinnerLogLoss(race);
        }).ToArray();
        var random = new Random(seed);
        var samples = new double[iterations];
        for (var iteration = 0; iteration < iterations; iteration++)
        {
            var total = 0d;
            for (var i = 0; i < differences.Length; i++) total += differences[random.Next(differences.Length)];
            samples[iteration] = total / differences.Length;
        }

        Array.Sort(samples);
        return samples[(int)Math.Floor(iterations * 0.025)];
    }

    private static double WinnerLogLoss(SettledRacePrediction race)
    {
        var probability = race.Probabilities.Single(x => x.StarterId == race.WinningStarterId).Probability;
        return -Math.Log(Math.Clamp(probability, Epsilon, 1 - Epsilon));
    }

    private static double ExpectedCalibrationError(List<(double Probability, bool Won)> values, int bins)
    {
        var total = values.Count;
        var error = 0d;
        for (var bin = 0; bin < bins; bin++)
        {
            var lower = (double)bin / bins;
            var upper = (double)(bin + 1) / bins;
            var rows = values.Where(x => x.Probability >= lower && (bin == bins - 1 ? x.Probability <= upper : x.Probability < upper)).ToArray();
            if (rows.Length == 0) continue;
            error += (double)rows.Length / total * Math.Abs(rows.Average(x => x.Probability) - rows.Average(x => x.Won ? 1d : 0d));
        }

        return error;
    }
}

public static class ChronologicalSplit
{
    public static (IReadOnlyList<T> Training, IReadOnlyList<T> Evaluation) EightyTwenty<T>(
        IEnumerable<T> source,
        Func<T, DateTimeOffset> timestamp)
    {
        var ordered = source.OrderBy(timestamp).ToArray();
        if (ordered.Length < 2) throw new ArgumentException("A chronological split requires at least two records.", nameof(source));
        var split = Math.Clamp((int)Math.Floor(ordered.Length * 0.8), 1, ordered.Length - 1);
        return (ordered[..split], ordered[split..]);
    }
}
