using RvglLiveScore.Models;
using RvglLiveScore.Services;

namespace RvglLiveScore.Tests;

public class QuickDrawTournamentTests
{
    private static RaceResult Race(params RaceEntry[] entries) => new("Track", entries);
    private static SessionResults Session(params RaceResult[] races) => new("Lobby", null, [], races);

    [Fact]
    public void UsesHeatAndFinalScoringTables()
    {
        Assert.Equal([10m, 9m, 8m, 7m, 6m, 5m, 4m, 3m, 2m, 1m, 0m],
            Enumerable.Range(1, 11).Select(place => QuickDrawTournamentCalculator.PointsForPlace(0, place)));
        Assert.Equal([30m, 25m, 22m, 19m, 17m, 15m, 14m, 13m, 12m, 11m, 10m, 9m, 8m, 7m, 6m, 5m, 4m, 3m, 2m, 1m, 0m],
            Enumerable.Range(1, 21).Select(place => QuickDrawTournamentCalculator.PointsForPlace(3, place)));
    }

    [Fact]
    public void ShowsEveryRaceSeparatelyAndSumsFourRaces()
    {
        var rows = QuickDrawTournamentCalculator.Calculate(Session(
            Race(new("A", 1, true), new("B", 2, true)),
            Race(new("B", 1, true), new("A", 2, true)),
            Race(new("A", 1, true), new("B", 2, true)),
            Race(new("B", 1, true), new("A", 2, true))));

        var b = rows.Single(row => row.Player == "B");
        var a = rows.Single(row => row.Player == "A");
        Assert.Equal([9m, 10m, 9m, 30m], b.RacePoints);
        Assert.Equal([10m, 9m, 10m, 25m], a.RacePoints);
        Assert.Equal(58, b.Points);
        Assert.Equal(54, a.Points);
        Assert.Equal(4, a.Diff);
        Assert.Equal(4, a.DiffLeader);
    }

    [Fact]
    public void UnplayedRacesAreBlankAndAbsentFinishersReceiveZero()
    {
        var rows = QuickDrawTournamentCalculator.Calculate(Session(
            Race(new("A", 1, true), new("B", 0, false)),
            Race(new("B", 1, true), new("A", 0, false))));

        Assert.Equal([10m, 0m, null, null], rows.Single(row => row.Player == "A").RacePoints);
        Assert.Equal([0m, 10m, null, null], rows.Single(row => row.Player == "B").RacePoints);
    }

    [Fact]
    public void IgnoresAbortedAndFifthRacesAndScoresReconnectOnce()
    {
        var aborted = new RaceResult("Track", [new("A", 0, false)], false);
        var rows = QuickDrawTournamentCalculator.Calculate(Session(
            Race(new("A", 1, true), new("A", 2, true)), aborted,
            Race(new RaceEntry("A", 1, true)), Race(new RaceEntry("A", 1, true)), Race(new RaceEntry("A", 1, true)),
            Race(new RaceEntry("B", 1, true))));

        var row = Assert.Single(rows);
        Assert.Equal([10m, 10m, 10m, 30m], row.RacePoints);
        Assert.Equal(60, row.Points);
    }

    [Fact]
    public void TiesUseWinsThenReverseOrdinalNames()
    {
        var rows = QuickDrawTournamentCalculator.Calculate(Session(
            Race(new("Aqua", 1, true), new("Abbe", 2, true)),
            Race(new("Abbe", 1, true), new("Aqua", 2, true))));

        Assert.Equal(["Aqua", "Abbe"], rows.Select(row => row.Player));
        Assert.Equal(0, rows[1].Diff);
        Assert.Equal(0, rows[1].DiffLeader);
    }
}
