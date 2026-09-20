using RvglLiveScore.Models;

namespace RvglLiveScore.Services;

public static class QuickDrawTournamentCalculator
{
    public const int RaceCount = 4;

    private static readonly decimal[] HeatPoints = [10, 9, 8, 7, 6, 5, 4, 3, 2, 1];
    private static readonly decimal[] FinalPoints =
        [30, 25, 22, 19, 17, 15, 14, 13, 12, 11, 10, 9, 8, 7, 6, 5, 4, 3, 2, 1];

    public static IReadOnlyList<QuickDrawStanding> Calculate(SessionResults session)
    {
        var races = session.Races
            .Where(race => race.IsComplete && race.Entries.Any(entry => entry.Finished && entry.Position > 0))
            .Take(RaceCount)
            .ToArray();
        var players = races.SelectMany(race => race.Entries)
            .Where(entry => entry.Finished && entry.Position > 0)
            .Select(entry => entry.Name)
            .Distinct(StringComparer.Ordinal)
            .ToDictionary(name => name, name => new Totals(name), StringComparer.Ordinal);

        for (var raceIndex = 0; raceIndex < races.Length; raceIndex++)
        {
            foreach (var player in players.Values) player.RacePoints[raceIndex] = 0;

            foreach (var entries in races[raceIndex].Entries.GroupBy(entry => entry.Name, StringComparer.Ordinal))
            {
                var entry = entries.Where(item => item.Finished && item.Position > 0)
                    .OrderBy(item => item.Position)
                    .FirstOrDefault();
                if (entry is null || !players.TryGetValue(entry.Name, out var totals)) continue;

                var points = PointsForPlace(raceIndex, entry.Position);
                totals.RacePoints[raceIndex] = points;
                totals.Points += points;
                if (entry.Position == 1) totals.Wins++;
            }
        }

        var sorted = players.Values
            .OrderByDescending(player => player.Points)
            .ThenByDescending(player => player.Wins)
            .ThenByDescending(player => player.Name, StringComparer.Ordinal)
            .ToArray();
        return sorted.Select((player, index) => new QuickDrawStanding(
            index + 1,
            player.Name,
            player.RacePoints,
            player.Points,
            index == 0 ? null : sorted[index - 1].Points - player.Points,
            index == 0 ? null : sorted[0].Points - player.Points)).ToArray();
    }

    public static decimal PointsForPlace(int raceIndex, int place)
    {
        if (place < 1) return 0;
        var points = raceIndex == RaceCount - 1 ? FinalPoints : HeatPoints;
        return place <= points.Length ? points[place - 1] : 0;
    }

    private sealed class Totals(string name)
    {
        public string Name { get; } = name;
        public decimal?[] RacePoints { get; } = new decimal?[RaceCount];
        public decimal Points { get; set; }
        public int Wins { get; set; }
    }
}
