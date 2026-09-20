using RvglLiveScore.Models;

namespace RvglLiveScore.Services;

public static class TmFinalistCalculator
{
    public static IReadOnlyList<TmStanding> Calculate(SessionResults session, ScoringSystem scoring, TmFinalistOptions options)
    {
        var races = session.Races.Where(race => race.IsComplete && race.Entries.Any(entry => entry.Finished && entry.Position > 0)).ToArray();
        var players = races.SelectMany(race => race.Entries).Where(entry => entry.Finished && entry.Position > 0)
            .Select(entry => entry.Name).Distinct(StringComparer.Ordinal)
            .ToDictionary(name => name, name => new Totals(name), StringComparer.Ordinal);
        var roundsInGroup = Math.Max(1, options.RoundsPerTrack) + (options.WarmupRound ? 1 : 0);
        var group = new List<RaceResult>();

        for (var raceIndex = 0; raceIndex < races.Length; raceIndex++)
        {
            var race = races[raceIndex];
            if (group.Count == roundsInGroup || (group.Count > 0 && group[0].Track != race.Track)) group.Clear();
            group.Add(race);
            var warmup = options.WarmupRound && group.Count == 1;
            if (!warmup)
            {
                foreach (var entries in race.Entries.GroupBy(entry => entry.Name, StringComparer.Ordinal))
                {
                    var entry = entries.Where(item => item.Finished && item.Position > 0)
                        .OrderBy(item => item.Position).FirstOrDefault() ?? entries.First();
                    if (!players.TryGetValue(entry.Name, out var totals)) continue;
                    var wasFinalist = totals.Status == TmFinalistStatus.Finalist;
                    var points = entry.Finished ? scoring.ForPlace(entry.Position, race.Entries.Count) : 0;
                    totals.Points += points;
                    if (entry.Finished && entry.Position == 1)
                    {
                        totals.Wins++;
                        if (wasFinalist)
                        {
                            totals.Status = TmFinalistStatus.Winner;
                            totals.WonAt = raceIndex;
                        }
                    }
                    if (totals.Status == TmFinalistStatus.Racing && totals.Points >= options.FinalistPoints)
                        totals.Status = TmFinalistStatus.Finalist;
                    if (raceIndex == races.Length - 1 && entry.Finished) totals.LastRoundParts.Add(points);
                }
            }

            if (group.Count == roundsInGroup)
            {
                var scoringRounds = options.WarmupRound ? group.Skip(1).ToArray() : group.ToArray();
                AwardTrackBonuses(scoringRounds, players, options, raceIndex == races.Length - 1);
            }
        }

        var currentGroup = CurrentTrackGroup(session, roundsInGroup);
        var currentRounds = options.WarmupRound ? currentGroup.Skip(1).ToArray() : currentGroup;
        var finishedRounds = currentRounds.Where(race => race.IsComplete && race.Entries.Any(entry => entry.Finished && entry.Position > 0)).ToArray();
        var bestLaps = currentRounds.SelectMany(race => race.Entries)
            .Where(entry => entry.BestLapMilliseconds is > 0)
            .GroupBy(entry => entry.Name, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Min(entry => entry.BestLapMilliseconds!.Value), StringComparer.Ordinal);
        var sorted = players.Values.OrderBy(player => player.Status switch
            {
                TmFinalistStatus.Winner => 0,
                TmFinalistStatus.Finalist => 1,
                _ => 2
            })
            .ThenBy(player => player.WonAt ?? int.MaxValue)
            .ThenByDescending(player => player.Points)
            .ThenByDescending(player => player.Wins)
            .ThenByDescending(player => player.Name, StringComparer.Ordinal).ToArray();
        return sorted.Select((player, index) => new TmStanding(
            index + 1, player.Name, player.Points, player.Status, player.LastRoundParts,
            bestLaps.TryGetValue(player.Name, out var lap) ? lap : null,
            finishedRounds.Length > 0 && finishedRounds.All(race => race.Entries.Any(entry =>
                entry.Name == player.Name && entry.Finished && entry.Position is >= 1 and <= 3)))).ToArray();
    }

    public static string FormatLap(int milliseconds) =>
        $"{milliseconds / 60000:00}:{milliseconds / 1000 % 60:00}:{milliseconds % 1000:000}";

    private static void AwardTrackBonuses(
        IReadOnlyList<RaceResult> rounds, Dictionary<string, Totals> players, TmFinalistOptions options, bool lastRound)
    {
        if (options.BestLapBonusEnabled && options.BestLapBonusPoints > 0)
        {
            var laps = rounds.SelectMany(round => round.Entries)
                .Where(entry => entry.BestLapMilliseconds is > 0 && players.ContainsKey(entry.Name)).ToArray();
            if (laps.Length > 0)
            {
                var fastest = laps.Min(entry => entry.BestLapMilliseconds!.Value);
                foreach (var name in laps.Where(entry => entry.BestLapMilliseconds == fastest)
                    .Select(entry => entry.Name).Distinct(StringComparer.Ordinal))
                    Award(players[name], options.BestLapBonusPoints, lastRound, options.FinalistPoints);
            }
        }
        if (options.PodiumBonusEnabled && options.PodiumBonusPoints > 0)
        {
            foreach (var player in players.Values)
                if (rounds.All(round => round.Entries.Any(entry => entry.Name == player.Name
                    && entry.Finished && entry.Position is >= 1 and <= 3)))
                    Award(player, options.PodiumBonusPoints, lastRound, options.FinalistPoints);
        }
    }

    private static void Award(Totals player, int bonus, bool lastRound, int finalistPoints)
    {
        player.Points += bonus;
        if (player.Status == TmFinalistStatus.Racing && player.Points >= finalistPoints)
            player.Status = TmFinalistStatus.Finalist;
        if (lastRound) player.LastRoundParts.Add(bonus);
    }

    private static RaceResult[] CurrentTrackGroup(SessionResults session, int roundsInGroup)
    {
        if (session.CurrentTrack is not { } track) return [];
        var group = new List<RaceResult>();
        foreach (var race in session.Races)
        {
            if (group.Count == roundsInGroup || (group.Count > 0 && group[0].Track != race.Track)) group.Clear();
            group.Add(race);
        }
        return group.Count > 0 && group[0].Track == track ? group.ToArray() : [];
    }

    private sealed class Totals(string name)
    {
        public string Name { get; } = name;
        public decimal Points { get; set; }
        public int Wins { get; set; }
        public TmFinalistStatus Status { get; set; }
        public int? WonAt { get; set; }
        public List<decimal> LastRoundParts { get; } = [];
    }
}
