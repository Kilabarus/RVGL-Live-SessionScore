using RvglLiveScore.Models;

namespace RvglLiveScore.Services;

public static class StandingsCalculator
{
    public static IReadOnlyList<Standing> Calculate(SessionResults session, ScoringSystem scoring)
    {
        // Completed races only. A race with no finishers is an aborted race.
        var races = session.Races.Where(x => x.IsComplete && x.Entries.Any(p => p.Finished && p.Position > 0)).ToArray();
        var players = races.SelectMany(x => x.Entries).Where(x => x.Finished && x.Position > 0).Select(x => x.Name)
            .Distinct(StringComparer.Ordinal).ToDictionary(x => x, x => new Totals(x), StringComparer.Ordinal);
        for (var raceIndex = 0; raceIndex < races.Length; raceIndex++)
        {
            foreach (var entries in races[raceIndex].Entries.GroupBy(x => x.Name, StringComparer.Ordinal))
            {
                // A reconnect can leave a DNF and a finished entry under the same name.
                // Keep every entry in the starter count, but score the name only once.
                var entry = entries.Where(x => x.Finished && x.Position > 0)
                    .OrderBy(x => x.Position).FirstOrDefault() ?? entries.First();
                if (!players.TryGetValue(entry.Name, out var totals)) continue;
                var points = entry.Finished ? scoring.ForPlace(entry.Position, races[raceIndex].Entries.Count) : 0;
                totals.Points += points;
                if (entry.Finished && entry.Position == 1) totals.Wins++;
                if (raceIndex == races.Length - 1) totals.LastRoundPoints = points;
            }
        }

        var sorted = players.Values.OrderByDescending(x => x.Points)
            .ThenByDescending(x => x.Wins)
            .ThenByDescending(x => x.Name, StringComparer.Ordinal).ToArray();
        return sorted.Select((player, index) => new Standing(index + 1, player.Name, player.Points,
            player.LastRoundPoints, player.Wins,
            index == 0 ? null : sorted[index - 1].Points - player.Points,
            index == 0 ? null : sorted[0].Points - player.Points)).ToArray();
    }

    private sealed class Totals(string name)
    {
        public string Name { get; } = name;
        public decimal Points { get; set; }
        public decimal LastRoundPoints { get; set; }
        public int Wins { get; set; }
    }
}
