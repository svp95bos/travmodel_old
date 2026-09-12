using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TravModel.Domain;

namespace TravModel.Infrastructure;

public sealed record StoredArtifact(string RelativePath, string Sha256, long SizeBytes);

public sealed class RawArtifactStore(string repositoryRoot)
{
    private readonly string root = Path.GetFullPath(repositoryRoot);

    public async Task<StoredArtifact> StoreAsync(
        string provider,
        DateTimeOffset retrievedAtUtc,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken)
    {
        var hash = Convert.ToHexString(SHA256.HashData(content.Span)).ToLowerInvariant();
        var relative = Path.Combine("data", "raw", Sanitize(provider), retrievedAtUtc.ToString("yyyy", System.Globalization.CultureInfo.InvariantCulture),
            retrievedAtUtc.ToString("MM", System.Globalization.CultureInfo.InvariantCulture), $"{hash}.json");
        var target = Path.GetFullPath(Path.Combine(root, relative));
        if (!target.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Resolved artifact path escaped the repository root.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        if (!File.Exists(target))
        {
            await File.WriteAllBytesAsync(target, content.ToArray(), cancellationToken).ConfigureAwait(false);
        }

        return new StoredArtifact(Path.GetRelativePath(root, target).Replace('\\', '/'), hash, content.Length);
    }

    private static string Sanitize(string value) => string.Concat(value.Select(x => char.IsLetterOrDigit(x) ? char.ToLowerInvariant(x) : '-'));
}

public sealed class CuratedJsonlExporter(TravDbContext dbContext, string repositoryRoot)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string root = Path.GetFullPath(repositoryRoot);

    public async Task<string> ExportAsync(DateOnly date, CancellationToken cancellationToken)
    {
        var start = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var end = start.AddDays(1);
        var races = await dbContext.Races.AsNoTracking()
            .Include(x => x.Starters)
            .Where(x => x.ScheduledStartUtc >= start && x.ScheduledStartUtc < end)
            .OrderBy(x => x.ScheduledStartUtc)
            .ThenBy(x => x.RaceNumber)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var raceIds = races.Select(x => x.Id).ToArray();
        var predictions = await dbContext.PredictionRuns.AsNoTracking()
            .Include(x => x.Predictions)
            .Where(x => raceIds.Contains(x.RaceId))
            .OrderBy(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var results = await dbContext.Results.AsNoTracking()
            .Where(x => raceIds.Contains(x.RaceId))
            .OrderBy(x => x.RaceId)
            .ThenBy(x => x.FinishPosition)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var directory = Path.Combine(root, "data", "curated", $"race_date={date:yyyy-MM-dd}");
        Directory.CreateDirectory(directory);
        var files = new List<object>
        {
            await WriteJsonLinesAsync(directory, "races.jsonl", races, cancellationToken).ConfigureAwait(false),
            await WriteJsonLinesAsync(directory, "predictions.jsonl", predictions, cancellationToken).ConfigureAwait(false),
            await WriteJsonLinesAsync(directory, "results.jsonl", results, cancellationToken).ConfigureAwait(false)
        };
        var manifestPath = Path.Combine(directory, "manifest.json");
        var manifest = new { schemaVersion = 1, raceDate = date, exportedAtUtc = DateTimeOffset.UtcNow, files };
        await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(manifest, JsonOptions), Encoding.UTF8, cancellationToken)
            .ConfigureAwait(false);
        return Path.GetRelativePath(root, manifestPath).Replace('\\', '/');
    }

    private static async Task<object> WriteJsonLinesAsync<T>(
        string directory,
        string fileName,
        IEnumerable<T> records,
        CancellationToken cancellationToken)
    {
        var path = Path.Combine(directory, fileName);
        await using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 65536, true))
        await using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
        {
            foreach (var record in records)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await writer.WriteLineAsync(JsonSerializer.Serialize(record, JsonOptions)).ConfigureAwait(false);
            }
        }

        var bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        return new
        {
            name = fileName,
            records = records.Count(),
            sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
            sizeBytes = bytes.LongLength
        };
    }
}
