using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using TravModel.Application;
using TravModel.Domain;
using TravModel.Infrastructure;
using TravModel.ML;

return await TravModelCommand.RunAsync(args, CancellationToken.None).ConfigureAwait(false);

internal static class TravModelCommand
{
    private const string Usage = """
        TravModel commands:
          doctor
          init-db
          sync-calendar --from yyyy-MM-dd --to yyyy-MM-dd
          backfill --from yyyy-MM-dd --to yyyy-MM-dd
          sync-results --from yyyy-MM-dd --to yyyy-MM-dd
          collect-due [--as-of ISO-8601]
          settle [--through ISO-8601]
          build-dataset --through ISO-8601 [--kind SportingOnly|MarketAware]
          train-challenger --through ISO-8601 [--kind SportingOnly|MarketAware]
          evaluate-and-promote --through ISO-8601 [--kind SportingOnly|MarketAware]
          export-curated --date yyyy-MM-dd
          demo-predict
        """;

    public static async Task<int> RunAsync(string[] args, CancellationToken cancellationToken)
    {
        if (args.Length == 0 || args[0] is "help" or "--help" or "-h")
        {
            Console.WriteLine(Usage);
            return 0;
        }

        try
        {
            var arguments = Arguments.Parse(args.Skip(1));
            return args[0] switch
            {
                "doctor" => Doctor(),
                "init-db" => await InitializeDatabaseAsync(cancellationToken).ConfigureAwait(false),
                "sync-calendar" or "backfill" => await SyncCalendarAsync(arguments, cancellationToken).ConfigureAwait(false),
                "sync-results" => await SyncResultsAsync(arguments, cancellationToken).ConfigureAwait(false),
                "collect-due" => await CollectDueAsync(arguments, cancellationToken).ConfigureAwait(false),
                "settle" => await SettleAsync(arguments, cancellationToken).ConfigureAwait(false),
                "build-dataset" => await BuildDatasetAsync(arguments, cancellationToken).ConfigureAwait(false),
                "train-challenger" or "evaluate-and-promote" => await TrainAndPromoteAsync(arguments, cancellationToken).ConfigureAwait(false),
                "export-curated" => await ExportAsync(arguments, cancellationToken).ConfigureAwait(false),
                "demo-predict" => DemoPredict(),
                _ => throw new ArgumentException($"Unknown command '{args[0]}'.\n{Usage}")
            };
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    private static int Doctor()
    {
        var checks = new[]
        {
            ("SQL connection", !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TRAVMODEL_SQL_CONNECTION"))),
            ("Svensk Travsport authorized gateway", ProviderOptions("SVENSKTRAVSPORT").Enabled && ProviderOptions("SVENSKTRAVSPORT").AuthorizationConfirmed),
            ("ATG authorized gateway", ProviderOptions("ATG").Enabled && ProviderOptions("ATG").AuthorizationConfirmed),
            ("SMHI adapter", ProviderOptions("SMHI").Enabled)
        };
        foreach (var (name, ready) in checks) Console.WriteLine($"{(ready ? "READY" : "NOT READY"),-9} {name}");
        Console.WriteLine("Live provider access is intentionally opt-in; synthetic tests and demo prediction need no credentials.");
        return checks[0].Item2 ? 0 : 2;
    }

    private static async Task<int> InitializeDatabaseAsync(CancellationToken cancellationToken)
    {
        await DatabaseBootstrap.InitializeAsync(ConnectionString(), cancellationToken).ConfigureAwait(false);
        Console.WriteLine("Database migrations applied.");
        return 0;
    }

    private static async Task<int> SyncCalendarAsync(Arguments arguments, CancellationToken cancellationToken)
    {
        var from = arguments.RequiredDate("from");
        var to = arguments.RequiredDate("to");
        await using var db = DatabaseBootstrap.CreateContext(ConnectionString());
        var store = new RawArtifactStore(RepositoryRoot());
        using var client = new HttpClient();
        var provider = new SvenskTravsportProvider(client, ProviderOptions("SVENSKTRAVSPORT"), store);
        var meetings = await provider.GetMeetingsAsync(from, to, cancellationToken).ConfigureAwait(false);
        var issues = ProviderDataValidator.ValidateMeetings(meetings, from, to);
        if (issues.Any(x => x.IsFatal))
            throw new InvalidOperationException(string.Join(Environment.NewLine, issues.Select(x => $"{x.Code}: {x.Message}")));
        var count = await new RaceIngestionService(db).ImportMeetingsAsync(meetings, cancellationToken).ConfigureAwait(false);
        Console.WriteLine($"Imported {count} new canonical records from {meetings.Count} meeting envelopes.");
        return 0;
    }

    private static async Task<int> SyncResultsAsync(Arguments arguments, CancellationToken cancellationToken)
    {
        var from = arguments.RequiredDate("from");
        var to = arguments.RequiredDate("to");
        await using var db = DatabaseBootstrap.CreateContext(ConnectionString());
        using var client = new HttpClient();
        var provider = new SvenskTravsportProvider(client, ProviderOptions("SVENSKTRAVSPORT"), new RawArtifactStore(RepositoryRoot()));
        var results = await provider.GetResultsAsync(from, to, cancellationToken).ConfigureAwait(false);
        var count = await new RaceIngestionService(db).ImportResultsAsync(results, cancellationToken).ConfigureAwait(false);
        Console.WriteLine($"Imported {count} official result rows.");
        return 0;
    }

    private static async Task<int> CollectDueAsync(Arguments arguments, CancellationToken cancellationToken)
    {
        var asOf = arguments.OptionalInstant("as-of") ?? DateTimeOffset.UtcNow;
        await using var db = DatabaseBootstrap.CreateContext(ConnectionString());
        var repository = new SqlTravRepository(db);
        var candidateRaces = await repository.GetRacesAsync(asOf, asOf.AddMinutes(1_445), cancellationToken).ConfigureAwait(false);
        var due = SnapshotPlanner.GetDueSnapshots(candidateRaces, asOf, TimeSpan.FromMinutes(5));
        var dueIds = due.Select(x => x.RaceId).ToHashSet();
        var races = candidateRaces.Where(x => dueIds.Contains(x.Id)).ToArray();
        if (races.Length == 0)
        {
            Console.WriteLine("No race snapshots are due.");
            return 0;
        }
        using var client = new HttpClient();
        var atgOptions = ProviderOptions("ATG");
        var market = atgOptions.Enabled && atgOptions.AuthorizationConfirmed
            ? await new AtgMarketProvider(client, atgOptions).GetMarketSnapshotAsync(races, asOf, cancellationToken).ConfigureAwait(false)
            : new BettingMarketSnapshot([], []);
        var smhiOptions = ProviderOptions("SMHI");
        var weather = smhiOptions.Enabled
            ? await new SmhiWeatherProvider(client).GetWeatherObservationsAsync(races, asOf, cancellationToken).ConfigureAwait(false)
            : [];
        await repository.AppendObservationsAsync([.. market.Observations, .. weather], cancellationToken).ConfigureAwait(false);
        await repository.SaveBettingMarketMembershipsAsync(market.Memberships, cancellationToken).ConfigureAwait(false);

        var activeModels = await db.ModelVersions.AsNoTracking()
            .Where(x => x.Status == ModelStatus.Active)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var predictionCount = 0;
        foreach (var version in activeModels)
        {
            if (version.Kind == ModelKind.MarketAware && market.Observations.Count == 0) continue;
            var model = new MlNetWinProbabilityModel(version.Id, version.Kind, Path.Combine(RepositoryRoot(), version.ArtifactPath));
            var service = new PredictionService(repository, model, new FixedClock(asOf));
            foreach (var race in races.Where(race => due.Any(x => x.RaceId == race.Id && x.MinutesBeforeStart == 15)))
            {
                await service.PredictAsync(race.Id, asOf, cancellationToken).ConfigureAwait(false);
                predictionCount++;
            }
        }

        Console.WriteLine($"Captured {market.Observations.Count} market observations, {market.Memberships.Count} memberships, and {weather.Count} weather observations; created {predictionCount} T-15 prediction runs for {races.Length} due races.");
        return 0;
    }

    private static async Task<int> SettleAsync(Arguments arguments, CancellationToken cancellationToken)
    {
        var through = arguments.OptionalInstant("through") ?? DateTimeOffset.UtcNow;
        await using var db = DatabaseBootstrap.CreateContext(ConnectionString());
        var count = await new PredictionSettlementService(db).SettleAsync(through, cancellationToken).ConfigureAwait(false);
        Console.WriteLine($"Settled {count} starter predictions.");
        return 0;
    }

    private static async Task<int> BuildDatasetAsync(Arguments arguments, CancellationToken cancellationToken)
    {
        var through = arguments.RequiredInstant("through");
        var kind = arguments.OptionalEnum("kind", ModelKind.SportingOnly);
        await using var db = DatabaseBootstrap.CreateContext(ConnectionString());
        var races = await BuildLabeledFeaturesAsync(db, through, cancellationToken).ConfigureAwait(false);
        var targetDirectory = Path.Combine(RepositoryRoot(), "data", "work");
        Directory.CreateDirectory(targetDirectory);
        var target = Path.Combine(targetDirectory, $"dataset-{kind.ToString().ToLowerInvariant()}-{through:yyyyMMddHHmmss}.json");
        await File.WriteAllTextAsync(target, JsonSerializer.Serialize(races, DatasetJsonOptions), cancellationToken).ConfigureAwait(false);
        Console.WriteLine($"Wrote {races.Count} labeled races to {Path.GetRelativePath(RepositoryRoot(), target)}.");
        return 0;
    }

    private static async Task<int> TrainAndPromoteAsync(Arguments arguments, CancellationToken cancellationToken)
    {
        var through = arguments.RequiredInstant("through");
        var kind = arguments.OptionalEnum("kind", ModelKind.SportingOnly);
        await using var db = DatabaseBootstrap.CreateContext(ConnectionString());
        var labeled = await BuildLabeledFeaturesAsync(db, through, cancellationToken).ConfigureAwait(false);
        var split = ChronologicalSplit.EightyTwenty(labeled, x => x.Features.CutoffUtc);
        var version = new ModelVersion
        {
            Version = $"{kind.ToString().ToLowerInvariant()}-{through:yyyyMMddHHmmss}",
            Kind = kind,
            Status = ModelStatus.Challenger,
            TrainedThroughUtc = split.Training.Max(x => x.Features.CutoffUtc),
            CreatedAtUtc = DateTimeOffset.UtcNow,
            TrainingDatasetFingerprint = DatasetFingerprint(split.Training),
            ArtifactPath = $"artifacts/models/{kind.ToString().ToLowerInvariant()}/{through:yyyyMMddHHmmss}.zip"
        };
        var artifact = Path.Combine(RepositoryRoot(), version.ArtifactPath);
        new MlNetWinModelTrainer().Train(split.Training, kind, version.Id, artifact);
        var challengerModel = new MlNetWinProbabilityModel(version.Id, kind, artifact);
        var challengerRaces = PredictSettled(challengerModel, split.Evaluation);
        var evaluation = PredictionMetrics.Evaluate(version.Id, challengerRaces,
            split.Evaluation.Min(x => x.Features.CutoffUtc), split.Evaluation.Max(x => x.Features.CutoffUtc));

        var incumbent = await db.ModelVersions.SingleOrDefaultAsync(
            x => x.Kind == kind && x.Status == ModelStatus.Active, cancellationToken).ConfigureAwait(false);
        SettledRacePrediction[] incumbentRaces;
        ModelEvaluation incumbentEvaluation;
        if (incumbent is null)
        {
            IWinProbabilityModel baseline = kind == ModelKind.MarketAware
                ? new MarketImpliedProbabilityModel(Guid.Empty)
                : new UniformWinProbabilityModel(Guid.Empty, kind);
            incumbentRaces = PredictSettled(baseline, split.Evaluation);
            incumbentEvaluation = PredictionMetrics.Evaluate(Guid.Empty, incumbentRaces,
                evaluation.WindowStartUtc, evaluation.WindowEndUtc);
        }
        else
        {
            var incumbentModel = new MlNetWinProbabilityModel(incumbent.Id, kind, Path.Combine(RepositoryRoot(), incumbent.ArtifactPath));
            incumbentRaces = PredictSettled(incumbentModel, split.Evaluation);
            incumbentEvaluation = PredictionMetrics.Evaluate(incumbent.Id, incumbentRaces,
                evaluation.WindowStartUtc, evaluation.WindowEndUtc);
        }

        evaluation.LogLossImprovementLower95 = PredictionMetrics.BootstrapLogLossImprovementLower95(incumbentRaces, challengerRaces);
        var decision = ModelPromotionPolicy.Evaluate(incumbentEvaluation, evaluation);
        version.Status = decision.Promote ? ModelStatus.Active : ModelStatus.Rejected;
        if (decision.Promote && incumbent is not null) incumbent.Status = ModelStatus.Retired;
        db.ModelVersions.Add(version);
        db.ModelEvaluations.Add(evaluation);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        Console.WriteLine(decision.Promote ? $"Promoted {version.Version}." : $"Rejected {version.Version}: {string.Join(" ", decision.Reasons)}");
        return decision.Promote ? 0 : 3;
    }

    private static async Task<int> ExportAsync(Arguments arguments, CancellationToken cancellationToken)
    {
        var date = arguments.RequiredDate("date");
        await using var db = DatabaseBootstrap.CreateContext(ConnectionString());
        var manifest = await new CuratedJsonlExporter(db, RepositoryRoot()).ExportAsync(date, cancellationToken).ConfigureAwait(false);
        Console.WriteLine($"Exported {manifest}.");
        return 0;
    }

    private static int DemoPredict()
    {
        var raceId = Guid.NewGuid();
        var features = new RaceFeatureSet(raceId, DateTimeOffset.UtcNow.AddMinutes(-15), "demo",
        [
            DemoVector(Guid.NewGuid(), 1, 1, 0.30f),
            DemoVector(Guid.NewGuid(), 2, 2, 0.15f),
            DemoVector(Guid.NewGuid(), 3, 3, 0.05f)
        ]);
        var model = new UniformWinProbabilityModel(Guid.Empty);
        foreach (var value in model.Predict(features))
            Console.WriteLine($"{value.Rank}: {value.StarterId} {value.Probability:P2}");
        return 0;
    }

    private static StarterFeatureVector DemoVector(Guid starterId, int number, int post, float recentWinRate) =>
        new(starterId, false, number, post, 2140, 3, 5, recentWinRate, 0.4f, 0.1f, 75, 0.12f, 50, 0.14f, 40, 0, 0, 10, 3);

    private static async Task<IReadOnlyList<LabeledRaceFeatureSet>> BuildLabeledFeaturesAsync(
        TravDbContext db,
        DateTimeOffset through,
        CancellationToken cancellationToken)
    {
        var completed = await db.Races.AsNoTracking()
            .Where(x => x.ScheduledStartUtc <= through)
            .Join(db.Results.Where(x => x.FinishPosition == 1), race => race.Id, result => result.RaceId,
                (race, result) => new { Race = race, WinnerId = result.StarterId })
            .OrderBy(x => x.Race.ScheduledStartUtc)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var repository = new SqlTravRepository(db);
        var output = new List<LabeledRaceFeatureSet>(completed.Count);
        foreach (var value in completed)
        {
            var cutoff = value.Race.ScheduledStartUtc.AddMinutes(-15);
            var input = await repository.GetFeatureInputAsync(value.Race.Id, cutoff, cancellationToken).ConfigureAwait(false);
            if (input is not null) output.Add(new LabeledRaceFeatureSet(PointInTimeFeatureBuilder.Build(input, cutoff), value.WinnerId));
        }

        return output;
    }

    private static SettledRacePrediction[] PredictSettled(
        IWinProbabilityModel model,
        IEnumerable<LabeledRaceFeatureSet> races) => races
        .Select(x => new SettledRacePrediction(x.Features.RaceId, x.WinningStarterId, model.Predict(x.Features)))
        .ToArray();

    private static string DatasetFingerprint(IEnumerable<LabeledRaceFeatureSet> races)
    {
        var text = string.Join('|', races.Select(x => x.Features.DatasetFingerprint).Order(StringComparer.Ordinal));
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    }

    private static string ConnectionString() =>
        Environment.GetEnvironmentVariable("TRAVMODEL_SQL_CONNECTION")
        ?? throw new InvalidOperationException("TRAVMODEL_SQL_CONNECTION is required.");

    private static string RepositoryRoot() => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    private static ProviderAccessOptions ProviderOptions(string name)
    {
        var prefix = $"TRAVMODEL_{name}_";
        return new ProviderAccessOptions
        {
            Enabled = bool.TryParse(Environment.GetEnvironmentVariable(prefix + "ENABLED"), out var enabled) && enabled,
            AuthorizationConfirmed = bool.TryParse(Environment.GetEnvironmentVariable(prefix + "AUTHORIZED"), out var authorized) && authorized,
            BaseUri = Uri.TryCreate(Environment.GetEnvironmentVariable(prefix + "BASE_URI"), UriKind.Absolute, out var uri) ? uri : null
        };
    }

    private static readonly JsonSerializerOptions DatasetJsonOptions = new()
    {
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
    };

    private sealed class FixedClock(DateTimeOffset value) : IClock
    {
        public DateTimeOffset UtcNow { get; } = value;
    }
}

internal sealed class Arguments
{
    private readonly Dictionary<string, string> values;

    private Arguments(Dictionary<string, string> values) => this.values = values;

    public static Arguments Parse(IEnumerable<string> arguments)
    {
        var items = arguments.ToArray();
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < items.Length; i += 2)
        {
            if (!items[i].StartsWith("--", StringComparison.Ordinal) || i + 1 >= items.Length)
                throw new ArgumentException($"Expected --name value near '{items[i]}'.");
            values[items[i][2..]] = items[i + 1];
        }

        return new Arguments(values);
    }

    public DateOnly RequiredDate(string name) => DateOnly.ParseExact(Required(name), "yyyy-MM-dd", CultureInfo.InvariantCulture);
    public DateTimeOffset RequiredInstant(string name) => DateTimeOffset.Parse(Required(name), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
    public DateTimeOffset? OptionalInstant(string name) => values.TryGetValue(name, out var value)
        ? DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal)
        : null;
    public T OptionalEnum<T>(string name, T fallback) where T : struct, Enum =>
        values.TryGetValue(name, out var value) ? Enum.Parse<T>(value, ignoreCase: true) : fallback;
    private string Required(string name) => values.TryGetValue(name, out var value)
        ? value
        : throw new ArgumentException($"--{name} is required.");
}
