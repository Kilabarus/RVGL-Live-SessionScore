using RvglLiveScore.Models;
using RvglLiveScore.Services;

namespace RvglLiveScore.Tests;

public class StandingsTests
{
    private static SessionResults Session(params RaceResult[] races) => new("Race", null, [], races);
    private static RaceResult Race(params RaceEntry[] entries) => new("Track", entries);
    private static ScoringSystem FixedSixteenToOne() => new(Enumerable.Range(1, 16)
        .Reverse().Select(x => (decimal)x).Concat(new decimal[ScoringSystem.PlaceCount - 16]));

    [Fact]
    public void CustomPointsDoNotDependOnNumberOfStarters()
    {
        var rows = StandingsCalculator.Calculate(Session(Race(new("Winner", 1, true), new("Second", 2, true))), FixedSixteenToOne());
        Assert.Equal(16, rows[0].Points);
        Assert.Equal(15, rows[1].Points);
        Assert.Null(rows[0].Diff);
        Assert.Null(rows[0].DiffLeader);
        Assert.Equal(1, rows[1].Diff);
        Assert.Equal(1, rows[1].DiffLeader);
    }

    [Fact]
    public void EqualPointsAndWinsUseReverseOrdinalPlayerNames()
    {
        var session = Session(Race(new("Aqua", 1, true), new("Abbe", 2, true)),
            Race(new("Abbe", 1, true), new("Aqua", 2, true)));
        var rows = StandingsCalculator.Calculate(session, FixedSixteenToOne());
        Assert.Equal(["Aqua", "Abbe"], rows.Select(x => x.Player));
        Assert.All(rows, x => Assert.Equal(31, x.Points));
        Assert.All(rows, x => Assert.Equal(1, x.Wins));
        Assert.Equal(0, rows[1].Diff);
        Assert.Equal(15, rows[0].LastRoundPoints);
        Assert.Equal(16, rows[1].LastRoundPoints);
    }

    [Fact]
    public void WinsBreakTiesBeforeNamesAndPointsBeforeWins()
    {
        var session = Session(Race(new("Alpha", 1, true), new("Zulu", 2, true), new("Points", 3, true)));
        var points = FixedSixteenToOne().Points.ToArray();
        points[0] = points[1] = 5;
        points[2] = 20;
        var rows = StandingsCalculator.Calculate(session, new(points));
        Assert.Equal(["Points", "Alpha", "Zulu"], rows.Select(x => x.Player));
        Assert.Equal(15, rows[1].Diff);
        Assert.Equal(0, rows[2].Diff);
        Assert.Equal(15, rows[2].DiffLeader);
    }

    [Fact]
    public void DnfAndAbsentPlayersEarnZeroAndAbortedRacesAreIgnored()
    {
        var session = Session(Race(new("A", 1, true), new("B", 1, false)),
            Race(new RaceEntry("B", 1, true)), Race(new("A", 0, false), new("B", 0, false)));
        var rows = StandingsCalculator.Calculate(session, FixedSixteenToOne());
        var a = rows.Single(x => x.Player == "A");
        var b = rows.Single(x => x.Player == "B");
        Assert.Equal(16, a.Points);
        Assert.Equal(16, b.Points);
        Assert.Equal(0, a.LastRoundPoints);
        Assert.Equal(16, b.LastRoundPoints);
        Assert.Equal(1, b.Wins);
    }

    [Fact]
    public void SameNameIsScoredOnlyOncePerRaceWhenMultipleEntriesFinish()
    {
        var session = Session(Race(new("Winner", 1, true), new("ROG", 2, true), new("ROG", 3, true)));
        var rows = StandingsCalculator.Calculate(session, FixedSixteenToOne());
        Assert.Equal(2, rows.Count);
        Assert.Equal(15, rows.Single(x => x.Player == "ROG").Points);
    }

    [Fact]
    public void ApplyingAnotherSystemRecalculatesAllRacesWithoutAccumulatingSnapshots()
    {
        var session = Session(Race(new RaceEntry("A", 1, true)), Race(new RaceEntry("A", 2, true)));
        var custom = FixedSixteenToOne().Points.ToArray();
        custom[0] = 36;
        custom[1] = 28;
        Assert.Equal(64, Assert.Single(StandingsCalculator.Calculate(session, new(custom))).Points);
        Assert.Equal(31, Assert.Single(StandingsCalculator.Calculate(session, FixedSixteenToOne())).Points);
        Assert.Equal(31, Assert.Single(StandingsCalculator.Calculate(session, FixedSixteenToOne())).Points);
    }

    [Fact]
    public void SixteenPlayersHaveConsecutivePositionsAndCorrectGaps()
    {
        var entries = Enumerable.Range(1, 16).Select(x => new RaceEntry($"Player {x}", x, true)).ToArray();
        var rows = StandingsCalculator.Calculate(Session(Race(entries)), FixedSixteenToOne());
        Assert.Equal(16, rows.Count);
        Assert.Equal(Enumerable.Range(1, 16), rows.Select(x => x.Position));
        Assert.Equal(15, rows[^1].DiffLeader);
        Assert.Equal(1, rows[^1].Points);
    }

    [Fact]
    public void CurrentRosterIsHiddenBeforeFirstFinish()
    {
        var rows = StandingsCalculator.Calculate(new("Lobby", null, ["Abbe", "Aqua"], []), ScoringSystem.CustomDefault);
        Assert.Empty(rows);
    }

    [Fact]
    public void FinisherWithZeroPointsIsShownWhileDnfAndSpectatorAreHidden()
    {
        var session = new SessionResults("Lobby", null, ["Finisher", "DNF", "Spectator"],
            [Race(new("Finisher", 1, true), new("DNF", 0, false))]);
        var row = Assert.Single(StandingsCalculator.Calculate(session, ScoringSystem.CustomDefault));
        Assert.Equal("Finisher", row.Player);
        Assert.Equal(0, row.Points);
    }

    [Fact]
    public void SignedAndDecimalScoringIsSupported()
    {
        var points = ScoringSystem.CustomDefault.Points.ToArray();
        points[0] = -1.5m;
        points[1] = 0;
        var rows = StandingsCalculator.Calculate(Session(Race(new("A", 1, true), new("B", 2, true))), new(points));
        Assert.Equal("B", rows[0].Player);
        Assert.Equal(1.5m, rows[1].Diff);
    }
}
