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
            await UpsertIdentityAsync("Track", track.Id, source, incoming.TrackExternalId, envelope.RetrievedAtUtc, cancellationToken).ConfigureAwait(false);

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
            await UpsertIdentityAsync("Meeting", meeting.Id, source, incoming.ExternalId, envelope.RetrievedAtUtc, cancellationToken).ConfigureAwait(false);

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
                await UpsertIdentityAsync("Race", race.Id, source, incomingRace.ExternalId, envelope.RetrievedAtUtc, cancellationToken).ConfigureAwait(false);
                AddObservation(run, "Race", race.Id, "ScheduledStartUtc", incomingRace.ScheduledStartUtc.ToUniversalTime().ToString("O"), envelope, true);
                AddObservation(run, "Race", race.Id, "DistanceMetres", incomingRace.DistanceMetres.ToString(System.Globalization.CultureInfo.InvariantCulture), envelope, true);
                AddObservation(run, "Race", race.Id, "StartMethod", incomingRace.StartMethod.ToString(), envelope, true);

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
                    await UpsertIdentityAsync("Horse", horse.Id, source, incomingStarter.HorseExternalId, envelope.RetrievedAtUtc, cancellationToken).ConfigureAwait(false);
                    await UpsertIdentityAsync("Person", driver.Id, source, incomingStarter.DriverExternalId, envelope.RetrievedAtUtc, cancellationToken).ConfigureAwait(false);
                    await UpsertIdentityAsync("Person", trainer.Id, source, incomingStarter.TrainerExternalId, envelope.RetrievedAtUtc, cancellationToken).ConfigureAwait(false);
                    await UpsertIdentityAsync("Starter", starter.Id, source, $"{incomingRace.ExternalId}:{incomingStarter.HorseExternalId}", envelope.RetrievedAtUtc, cancellationToken).ConfigureAwait(false);
                    AddObservation(run, "Starter", starter.Id, "DriverId", driver.Id.ToString("D"), envelope, true);
                    AddObservation(run, "Starter", starter.Id, "TrainerId", trainer.Id.ToString("D"), envelope, true);
                    AddObservation(run, "Starter", starter.Id, "HorseNumber", incomingStarter.HorseNumber.ToString(System.Globalization.CultureInfo.InvariantCulture), envelope, true);
                    AddObservation(run, "Starter", starter.Id, "PostPosition", incomingStarter.PostPosition.ToString(System.Globalization.CultureInfo.InvariantCulture), envelope, true);
                    AddObservation(run, "Starter", starter.Id, "DistanceMetres", incomingStarter.DistanceMetres.ToString(System.Globalization.CultureInfo.InvariantCulture), envelope, true);
                    AddObservation(run, "Starter", starter.Id, "IsScratched", incomingStarter.IsScratched.ToString(System.Globalization.CultureInfo.InvariantCulture), envelope, true);
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
                .Include(x => x.Meeting).ThenInclude(x => x!.Track)
                .SingleOrDefaultAsync(x => x.ExternalSource == envelope.SourceName && x.ExternalId == incoming.RaceExternalId, cancellationToken)
                .ConfigureAwait(false);
            if (race is null) continue;
            var starter = race.Starters.SingleOrDefault(x => x.Horse?.ExternalId == incoming.HorseExternalId);
            if (starter is null) continue;
            var result = await dbContext.Results.SingleOrDefaultAsync(x => x.RaceId == race.Id && x.StarterId == starter.Id,
                cancellationToken).ConfigureAwait(false);
            if (result is null)
            {
                result = new RaceResult
                {
                    RaceId = race.Id,
                    StarterId = starter.Id,
                    FinishCode = incoming.FinishCode,
                    SourceName = envelope.SourceName,
                    SourceUrl = envelope.SourceUrl.ToString(),
                    RetrievedAtUtc = envelope.RetrievedAtUtc,
                    PublishedAtUtc = envelope.ObservedAtUtc
                };
                dbContext.Results.Add(result);
                written++;
            }
            result.FinishPosition = incoming.FinishPosition;
            result.FinishCode = incoming.FinishCode;
            result.KilometerTimeSeconds = incoming.KilometerTimeSeconds;
            result.WinningMarginMetres = incoming.WinningMarginMetres;
            result.PrizeMoneySek = incoming.PrizeMoneySek;
            result.Galloped = incoming.Galloped;
            result.RaceComment = incoming.RaceComment;
            result.SourceName = envelope.SourceName;
            result.SourceUrl = envelope.SourceUrl.ToString();
            result.RetrievedAtUtc = envelope.RetrievedAtUtc;
            result.PublishedAtUtc = envelope.ObservedAtUtc;
            AddObservation(run, "Starter", starter.Id, "FinishPosition", incoming.FinishPosition?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty, envelope, true);
            AddObservation(run, "Starter", starter.Id, "FinishCode", incoming.FinishCode, envelope, true);
            AddObservation(run, "Starter", starter.Id, "KilometerTimeSeconds", incoming.KilometerTimeSeconds?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty, envelope, true);
            AddObservation(run, "Starter", starter.Id, "PrizeMoneySek", incoming.PrizeMoneySek?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty, envelope, true);
            AddObservation(run, "Starter", starter.Id, "Galloped", incoming.Galloped.ToString(System.Globalization.CultureInfo.InvariantCulture), envelope, true);

            var canonicalStartKey = race.Id.ToString("D");
            var historical = await dbContext.HistoricalStarts.SingleOrDefaultAsync(x =>
                x.HorseId == starter.HorseId && x.CanonicalStartKey == canonicalStartKey, cancellationToken).ConfigureAwait(false);
            if (historical is null)
            {
                historical = new HistoricalStart
                {
                    HorseId = starter.HorseId,
                    DriverId = starter.DriverId,
                    TrainerId = starter.TrainerId,
                    ExternalRaceId = incoming.RaceExternalId,
                    CanonicalStartKey = canonicalStartKey,
                    StartTimeUtc = race.ScheduledStartUtc,
                    TrackName = race.Meeting?.Track?.Name ?? "Unknown",
                    RaceNumber = race.RaceNumber,
                    DistanceMetres = starter.DistanceMetres,
                    StartMethod = race.StartMethod,
                    PostPosition = starter.PostPosition,
                    FinishPosition = incoming.FinishPosition,
                    KilometerTimeSeconds = incoming.KilometerTimeSeconds,
                    PrizeMoneySek = incoming.PrizeMoneySek,
                    Galloped = incoming.Galloped,
                    RaceComment = incoming.RaceComment,
                    SourceName = envelope.SourceName,
                    SourceUrl = envelope.SourceUrl.ToString(),
                    RetrievedAtUtc = envelope.RetrievedAtUtc,
                    ObservedAtUtc = envelope.ObservedAtUtc,
                    FirstSeenAtUtc = envelope.RetrievedAtUtc,
                    LastSeenAtUtc = envelope.RetrievedAtUtc,
                    CompletenessFlags = "Odds,Shoes,Sulky,TrackCondition"
                };
                dbContext.HistoricalStarts.Add(historical);
                written++;
            }
            else
            {
                historical.FinishPosition = incoming.FinishPosition;
                historical.KilometerTimeSeconds = incoming.KilometerTimeSeconds;
                historical.PrizeMoneySek = incoming.PrizeMoneySek;
                historical.Galloped = incoming.Galloped;
                historical.RaceComment = incoming.RaceComment;
                historical.LastSeenAtUtc = envelope.RetrievedAtUtc;
                historical.SourceName = envelope.SourceName;
                historical.SourceUrl = envelope.SourceUrl.ToString();
            }
            var recent = new ProviderRecentStart(incoming.HorseExternalId, incoming.RaceExternalId, race.ScheduledStartUtc,
                race.Meeting?.Track?.Name ?? "Unknown", race.RaceNumber, starter.DistanceMetres, race.StartMethod,
                starter.PostPosition, incoming.FinishPosition, incoming.KilometerTimeSeconds, null, null, null, null,
                incoming.PrizeMoneySek, incoming.Galloped, incoming.RaceComment,
                starter.Driver?.ExternalId, starter.Trainer?.ExternalId);
            var recentEnvelope = new SourceEnvelope<ProviderRecentStart>(recent, envelope.SourceName, envelope.SourceUrl,
                envelope.RetrievedAtUtc, envelope.ObservedAtUtc, envelope.RawPayload, envelope.RawArtifact);
            var revision = HistoricalStartRevisionFactory.Create(historical, recent, recentEnvelope);
            if (!await dbContext.HistoricalStartRevisions.AnyAsync(x => x.ContentHash == revision.ContentHash, cancellationToken).ConfigureAwait(false))
            {
                dbContext.HistoricalStartRevisions.Add(revision);
                written++;
            }
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

    private async Task UpsertIdentityAsync(
        string entityType,
        Guid entityId,
        string source,
        string externalId,
        DateTimeOffset seenAtUtc,
        CancellationToken cancellationToken)
    {
        var identity = dbContext.ExternalIdentities.Local.SingleOrDefault(x =>
            x.EntityType == entityType && x.SourceName == source && x.ExternalId == externalId)
            ?? await dbContext.ExternalIdentities.SingleOrDefaultAsync(x =>
                x.EntityType == entityType && x.SourceName == source && x.ExternalId == externalId,
                cancellationToken).ConfigureAwait(false);
        if (identity is null)
        {
            dbContext.ExternalIdentities.Add(new ExternalIdentity
            {
                EntityType = entityType,
                EntityId = entityId,
                SourceName = source,
                ExternalId = externalId,
                FirstSeenAtUtc = seenAtUtc,
                LastSeenAtUtc = seenAtUtc,
                IsVerified = true
            });
            return;
        }

        if (identity.EntityId != entityId)
            throw new InvalidDataException($"Identity conflict for {entityType} {source}:{externalId}.");
        if (seenAtUtc > identity.LastSeenAtUtc) identity.LastSeenAtUtc = seenAtUtc;
    }

    private void AddObservation<T>(
        IngestionRun run,
        string entityType,
        Guid entityId,
        string field,
        string value,
        SourceEnvelope<T> envelope,
        bool authoritative)
    {
        var observation = ObservationFactory.Create(entityType, entityId.ToString("D"), field, envelope.SourceName,
            envelope.SourceUrl.ToString(), envelope.RetrievedAtUtc, envelope.ObservedAtUtc, value, value, authoritative);
        observation.IngestionRun = run;
        dbContext.Observations.Add(observation);
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
