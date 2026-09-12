using Microsoft.EntityFrameworkCore;
using TravModel.Application;
using TravModel.Domain;

namespace TravModel.Infrastructure;

public sealed class RaceIngestionService(TravDbContext dbContext)
{
    public async Task<int> ImportMeetingsAsync(
        IReadOnlyCollection<SourceEnvelope<ProviderMeeting>> envelopes,
        CancellationToken cancellationToken)
    {
        var run = BeginRun(envelopes.FirstOrDefault()?.SourceName ?? "Unknown", envelopes.Count);
        var written = 0;
        foreach (var envelope in envelopes)
        {
            var source = envelope.SourceName;
            var incoming = envelope.Value;
            var track = await dbContext.Tracks.SingleOrDefaultAsync(
                x => x.ExternalSource == source && x.ExternalId == incoming.TrackExternalId,
                cancellationToken).ConfigureAwait(false);
            if (track is null)
            {
                track = new Track
                {
                    ExternalSource = source,
                    ExternalId = incoming.TrackExternalId,
                    Name = incoming.TrackName,
                    Latitude = incoming.TrackLatitude,
                    Longitude = incoming.TrackLongitude
                };
                dbContext.Tracks.Add(track);
                written++;
            }
            else
            {
                track.Name = incoming.TrackName;
                track.Latitude = incoming.TrackLatitude ?? track.Latitude;
                track.Longitude = incoming.TrackLongitude ?? track.Longitude;
            }

            var meeting = await dbContext.Meetings.SingleOrDefaultAsync(
                x => x.ExternalSource == source && x.ExternalId == incoming.ExternalId,
                cancellationToken).ConfigureAwait(false);
            if (meeting is null)
            {
                meeting = new Meeting
                {
                    ExternalSource = source,
                    ExternalId = incoming.ExternalId,
                    Track = track,
                    MeetingDate = incoming.Date
                };
                dbContext.Meetings.Add(meeting);
                written++;
            }

            foreach (var incomingRace in incoming.Races)
            {
                var race = await dbContext.Races.Include(x => x.Starters).SingleOrDefaultAsync(
                    x => x.ExternalSource == source && x.ExternalId == incomingRace.ExternalId,
                    cancellationToken).ConfigureAwait(false);
                if (race is null)
                {
                    race = new Race
                    {
                        ExternalSource = source,
                        ExternalId = incomingRace.ExternalId,
                        Meeting = meeting,
                        RaceNumber = incomingRace.RaceNumber,
                        ScheduledStartUtc = incomingRace.ScheduledStartUtc,
                        DistanceMetres = incomingRace.DistanceMetres,
                        StartMethod = incomingRace.StartMethod,
                        RaceType = incomingRace.RaceType,
                        ConditionsText = incomingRace.ConditionsText
                    };
                    dbContext.Races.Add(race);
                    written++;
                }
                else
                {
                    race.ScheduledStartUtc = incomingRace.ScheduledStartUtc;
                    race.DistanceMetres = incomingRace.DistanceMetres;
                    race.StartMethod = incomingRace.StartMethod;
                    race.RaceType = incomingRace.RaceType;
                    race.ConditionsText = incomingRace.ConditionsText;
                }

                foreach (var incomingStarter in incomingRace.Starters)
                {
                    var horse = await GetOrCreateHorseAsync(source, incomingStarter, cancellationToken).ConfigureAwait(false);
                    var driver = await GetOrCreatePersonAsync(source, incomingStarter.DriverExternalId, incomingStarter.DriverName, cancellationToken).ConfigureAwait(false);
                    var trainer = await GetOrCreatePersonAsync(source, incomingStarter.TrainerExternalId, incomingStarter.TrainerName, cancellationToken).ConfigureAwait(false);
                    var starter = race.Starters.SingleOrDefault(x => x.HorseId == horse.Id || x.Horse == horse);
                    if (starter is null)
                    {
                        starter = new Starter { Race = race, Horse = horse, Driver = driver, Trainer = trainer };
                        race.Starters.Add(starter);
                        written++;
                    }

                    starter.Driver = driver;
                    starter.Trainer = trainer;
                    starter.HorseNumber = incomingStarter.HorseNumber;
                    starter.PostPosition = incomingStarter.PostPosition;
                    starter.DistanceMetres = incomingStarter.DistanceMetres;
                    starter.IsScratched = incomingStarter.IsScratched;
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        CompleteRun(run, written);
        AddArtifactManifests(run, envelopes.Select(x => (x.SourceName, x.SourceUrl, x.RetrievedAtUtc, x.RawArtifact)));
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return written;
    }

    public async Task<int> ImportResultsAsync(
        IReadOnlyCollection<SourceEnvelope<ProviderResult>> envelopes,
        CancellationToken cancellationToken)
    {
        var run = BeginRun(envelopes.FirstOrDefault()?.SourceName ?? "Unknown", envelopes.Count);
        var written = 0;
        foreach (var envelope in envelopes)
        {
            var incoming = envelope.Value;
            var race = await dbContext.Races
                .Include(x => x.Starters).ThenInclude(x => x.Horse)
                .SingleOrDefaultAsync(x => x.ExternalSource == envelope.SourceName && x.ExternalId == incoming.RaceExternalId, cancellationToken)
                .ConfigureAwait(false);
            if (race is null) continue;
            var starter = race.Starters.SingleOrDefault(x => x.Horse?.ExternalId == incoming.HorseExternalId);
            if (starter is null) continue;
            if (await dbContext.Results.AnyAsync(x => x.RaceId == race.Id && x.StarterId == starter.Id, cancellationToken).ConfigureAwait(false))
            {
                continue;
            }

            dbContext.Results.Add(new RaceResult
            {
                RaceId = race.Id,
                StarterId = starter.Id,
                FinishPosition = incoming.FinishPosition,
                FinishCode = incoming.FinishCode,
                KilometerTimeSeconds = incoming.KilometerTimeSeconds,
                WinningMarginMetres = incoming.WinningMarginMetres,
                PrizeMoneySek = incoming.PrizeMoneySek,
                Galloped = incoming.Galloped,
                RaceComment = incoming.RaceComment,
                SourceName = envelope.SourceName,
                SourceUrl = envelope.SourceUrl.ToString(),
                RetrievedAtUtc = envelope.RetrievedAtUtc,
                PublishedAtUtc = envelope.ObservedAtUtc
            });
            written++;
        }

        CompleteRun(run, written);
        AddArtifactManifests(run, envelopes.Select(x => (x.SourceName, x.SourceUrl, x.RetrievedAtUtc, x.RawArtifact)));
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return written;
    }

    private IngestionRun BeginRun(string provider, int recordsRead)
    {
        var run = new IngestionRun
        {
            Provider = provider,
            StartedAtUtc = DateTimeOffset.UtcNow,
            Status = "Running",
            RecordsRead = recordsRead
        };
        dbContext.IngestionRuns.Add(run);
        return run;
    }

    private static void CompleteRun(IngestionRun run, int recordsWritten)
    {
        run.CompletedAtUtc = DateTimeOffset.UtcNow;
        run.Status = "Completed";
        run.RecordsWritten = recordsWritten;
    }

    private void AddArtifactManifests(
        IngestionRun run,
        IEnumerable<(string SourceName, Uri SourceUrl, DateTimeOffset RetrievedAtUtc, RawArtifactReference? Artifact)> artifacts)
    {
        foreach (var value in artifacts.Where(x => x.Artifact is not null).DistinctBy(x => x.Artifact!.Sha256))
        {
            dbContext.RawArtifactManifests.Add(new RawArtifactManifest
            {
                IngestionRun = run,
                Provider = value.SourceName,
                SourceUrl = value.SourceUrl.ToString(),
                RelativePath = value.Artifact!.RelativePath,
                Sha256 = value.Artifact.Sha256,
                RetrievedAtUtc = value.RetrievedAtUtc,
                SizeBytes = value.Artifact.SizeBytes
            });
        }
    }

    private async Task<Horse> GetOrCreateHorseAsync(string source, ProviderStarter starter, CancellationToken cancellationToken)
    {
        var horse = await dbContext.Horses.SingleOrDefaultAsync(
            x => x.ExternalSource == source && x.ExternalId == starter.HorseExternalId,
            cancellationToken).ConfigureAwait(false);
        if (horse is not null) return horse;
        horse = new Horse
        {
            ExternalSource = source,
            ExternalId = starter.HorseExternalId,
            Name = starter.HorseName,
            Sex = "Unknown"
        };
        dbContext.Horses.Add(horse);
        return horse;
    }

    private async Task<Person> GetOrCreatePersonAsync(
        string source,
        string externalId,
        string name,
        CancellationToken cancellationToken)
    {
        var person = await dbContext.People.SingleOrDefaultAsync(
            x => x.ExternalSource == source && x.ExternalId == externalId,
            cancellationToken).ConfigureAwait(false);
        if (person is not null) return person;
        person = new Person { ExternalSource = source, ExternalId = externalId, Name = name };
        dbContext.People.Add(person);
        return person;
    }
}

public sealed class PredictionSettlementService(TravDbContext dbContext)
{
    public async Task<int> SettleAsync(DateTimeOffset throughUtc, CancellationToken cancellationToken)
    {
        var runs = await dbContext.PredictionRuns
            .Include(x => x.Predictions)
            .Where(x => x.CreatedAtUtc <= throughUtc && x.Predictions.Any(p => p.SettledAtUtc == null))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        if (runs.Count == 0) return 0;
        var raceIds = runs.Select(x => x.RaceId).Distinct().ToArray();
        var winners = await dbContext.Results
            .Where(x => raceIds.Contains(x.RaceId) && x.FinishPosition == 1)
            .ToDictionaryAsync(x => x.RaceId, x => x.StarterId, cancellationToken)
            .ConfigureAwait(false);
        var settled = 0;
        foreach (var run in runs.Where(x => winners.ContainsKey(x.RaceId)))
        {
            foreach (var prediction in run.Predictions)
            {
                prediction.Won = prediction.StarterId == winners[run.RaceId];
                prediction.SettledAtUtc = throughUtc;
                settled++;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return settled;
    }
}
