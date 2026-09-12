namespace TravModel.Application;

public sealed record ValidationIssue(string Code, string Message, bool IsFatal);

public static class ProviderDataValidator
{
    public static IReadOnlyList<ValidationIssue> ValidateMeetings(
        IReadOnlyCollection<SourceEnvelope<ProviderMeeting>> meetings,
        DateOnly requestedFrom,
        DateOnly requestedTo)
    {
        var issues = new List<ValidationIssue>();
        foreach (var duplicate in meetings.GroupBy(x => (x.SourceName, x.Value.ExternalId)).Where(x => x.Count() > 1))
            issues.Add(new ValidationIssue("duplicate-meeting", $"Duplicate meeting {duplicate.Key.ExternalId} from {duplicate.Key.SourceName}.", true));
        foreach (var envelope in meetings)
        {
            var meeting = envelope.Value;
            if (meeting.Date < requestedFrom || meeting.Date > requestedTo)
                issues.Add(new ValidationIssue("date-mismatch", $"Meeting {meeting.ExternalId} is dated {meeting.Date:yyyy-MM-dd} outside the requested range.", true));
            if (string.IsNullOrWhiteSpace(meeting.TrackExternalId) || string.IsNullOrWhiteSpace(meeting.TrackName))
                issues.Add(new ValidationIssue("track-missing", $"Meeting {meeting.ExternalId} has no stable track identity.", true));
            foreach (var race in meeting.Races)
            {
                if (race.RaceNumber <= 0)
                    issues.Add(new ValidationIssue("race-number", $"Race {race.ExternalId} has invalid race number {race.RaceNumber}.", true));
                if (race.Starters.Count == 0)
                    issues.Add(new ValidationIssue("missing-starters", $"Race {race.ExternalId} contains no starters.", true));
                foreach (var duplicateStarter in race.Starters.GroupBy(x => x.HorseExternalId).Where(x => x.Count() > 1))
                    issues.Add(new ValidationIssue("duplicate-starter", $"Race {race.ExternalId} repeats horse {duplicateStarter.Key}.", true));
            }

            foreach (var duplicateNumber in meeting.Races.GroupBy(x => x.RaceNumber).Where(x => x.Count() > 1))
                issues.Add(new ValidationIssue("race-number-mismatch", $"Meeting {meeting.ExternalId} repeats race number {duplicateNumber.Key}.", true));
        }

        return issues;
    }
}

public sealed record DueSnapshot(Guid RaceId, int MinutesBeforeStart);

public static class SnapshotPlanner
{
    private static readonly int[] Horizons = [1440, 360, 60, 15, 5];

    public static IReadOnlyList<DueSnapshot> GetDueSnapshots(
        IEnumerable<TravModel.Domain.Race> races,
        DateTimeOffset asOfUtc,
        TimeSpan pollingInterval)
    {
        var intervalMinutes = pollingInterval.TotalMinutes;
        return races.SelectMany(race => Horizons
                .Where(horizon =>
                {
                    var minutes = (race.ScheduledStartUtc - asOfUtc).TotalMinutes;
                    return minutes >= horizon && minutes < horizon + intervalMinutes;
                })
                .Select(horizon => new DueSnapshot(race.Id, horizon)))
            .ToArray();
    }
}

public sealed record DataCoverageReport(
    DateTimeOffset GeneratedAtUtc,
    DateTimeOffset WindowStartUtc,
    DateTimeOffset WindowEndUtc,
    int Races,
    int Starters,
    int StartersWithThreeRecentStarts,
    int StartersWithFiveRecentStarts,
    int StartersWithEquipment,
    int PendingIdentityReviews,
    int FailedIngestionRuns,
    IReadOnlyDictionary<string, int> ObservationsBySource,
    IReadOnlyList<string> ProviderAlerts);
