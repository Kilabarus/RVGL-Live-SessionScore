using RvglLiveScore.Models;
using RvglLiveScore.Services;

namespace RvglLiveScore.Tests;

public class TmFinalistTests
{
    private static readonly ScoringSystem Scoring = new(
        [60m, 30m, 20m, 10m, .. new decimal[ScoringSystem.PlaceCount - 4]]);

    private static RaceResult Race(string track, params RaceEntry[] entries) => new(track, entries);
    private static SessionResults Session(params RaceResult[] races) => new("Lobby", "Track", [], races);
    private static TmFinalistOptions NoBonuses => new(BestLapBonusEnabled: false);

    [Fact]
    public void WinnerRequiresVictoryAfterTheRaceThatReachedFinalistThreshold()
    {
        var first = Race("Track", new("A", 1, true), new("B", 2, true));
        var second = Race("Track", new("A", 1, true), new("B", 2, true));
        var third = Race("Track", new("A", 1, true), new("B", 2, true));
        Assert.Equal(TmFinalistStatus.Finalist,
            TmFinalistCalculator.Calculate(Session(first, second), Scoring, NoBonuses).Single(x => x.Player == "A").Status);
        var winner = TmFinalistCalculator.Calculate(Session(first, second, third), Scoring, NoBonuses).Single(x => x.Player == "A");
        Assert.Equal(TmFinalistStatus.Winner, winner.Status);
        Assert.Equal(1, winner.Position);
    }

    [Fact]
    public void BestLapBonusIsAwardedAtTrackEndAndShownInLastRoundPoints()
    {
        var race = Race("Track", new("A", 1, true, 42387), new("B", 2, true, 43608));
        var options = new TmFinalistOptions(RoundsPerTrack: 1, BestLapBonusPoints: 32);
        var rows = TmFinalistCalculator.Calculate(Session(race), Scoring, options);
        var a = rows.Single(x => x.Player == "A");
        Assert.Equal(92, a.Points);
        Assert.Equal([60m, 32m], a.LastRoundParts);
        Assert.Equal(42387, a.BestLapMilliseconds);
        Assert.Equal("00:42:387", TmFinalistCalculator.FormatLap(Assert.IsType<int>(a.BestLapMilliseconds)));
        Assert.Equal([30m], rows.Single(x => x.Player == "B").LastRoundParts);
    }

    [Fact]
    public void WarmupIsUnscoredAndPodiumBonusRequiresEveryScoringRound()
    {
        var warmup = Race("Track", new("A", 4, true), new("B", 1, true));
        var round1 = Race("Track", new("A", 1, true), new("B", 4, true));
        var round2 = Race("Track", new("A", 2, true), new("B", 1, true));
        var options = new TmFinalistOptions(RoundsPerTrack: 2, WarmupRound: true,
            BestLapBonusEnabled: false, PodiumBonusEnabled: true, PodiumBonusPoints: 16);
        var beforeEnd = TmFinalistCalculator.Calculate(Session(warmup, round1), Scoring, options);
        Assert.Equal(60, beforeEnd.Single(x => x.Player == "A").Points);
        var rows = TmFinalistCalculator.Calculate(Session(warmup, round1, round2), Scoring, options);
        var a = rows.Single(x => x.Player == "A");
        Assert.Equal(106, a.Points);
        Assert.Equal([30m, 16m], a.LastRoundParts);
        Assert.True(a.PodiumEligible);
        Assert.False(rows.Single(x => x.Player == "B").PodiumEligible);
        Assert.Equal(70, rows.Single(x => x.Player == "B").Points);
    }

    [Fact]
    public void WinnersRankAheadOfFinalistsEvenWithFewerPoints()
    {
        var scoring = new ScoringSystem([60m, 55m, .. new decimal[ScoringSystem.PlaceCount - 2]]);
        var races = new[]
        {
            Race("Track", new("B", 1, true), new("A", 2, true)),
            Race("Track", new("B", 1, true), new("A", 2, true)),
            Race("Track", new("A", 1, true), new("B", 2, true))
        };
        var rows = TmFinalistCalculator.Calculate(Session(races), scoring, NoBonuses);
        Assert.Equal(TmFinalistStatus.Winner, rows[0].Status);
        Assert.Equal("A", rows[0].Player);
        Assert.Equal(TmFinalistStatus.Finalist, rows[1].Status);
        Assert.True(rows[0].Points < rows[1].Points);
    }

    [Fact]
    public void TwoPlayersCanBecomeWinnersInDifferentLaterRounds()
    {
        var scoring = new ScoringSystem([60m, 55m, .. new decimal[ScoringSystem.PlaceCount - 2]]);
        var races = new[]
        {
            Race("Track", new("A", 1, true), new("B", 2, true)),
            Race("Track", new("B", 1, true), new("A", 2, true)),
            Race("Track", new("A", 1, true), new("B", 2, true)),
            Race("Track 2", new("B", 1, true), new("A", 2, true))
        };
        var rows = TmFinalistCalculator.Calculate(Session(races), scoring, NoBonuses);
        Assert.Equal(["A", "B"], rows.Select(row => row.Player));
        Assert.All(rows, row => Assert.Equal(TmFinalistStatus.Winner, row.Status));
    }

    [Fact]
    public void LiveLapAppearsWithoutScoringUnfinishedRace()
    {
        var completed = Race("Track", new("A", 1, true, 45000), new("B", 2, true, 46000));
        var live = new RaceResult("Track", [new("A", 0, false, 42000), new("B", 0, false, 43000)], false);
        var rows = TmFinalistCalculator.Calculate(Session(completed, live), Scoring, NoBonuses);
        Assert.Equal(60, rows.Single(row => row.Player == "A").Points);
        Assert.Equal(42000, rows.Single(row => row.Player == "A").BestLapMilliseconds);
        Assert.Equal(43000, rows.Single(row => row.Player == "B").BestLapMilliseconds);
    }
}
