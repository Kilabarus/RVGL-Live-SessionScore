using RvglLiveScore.Models;
using RvglLiveScore.Services;

namespace RvglLiveScore.Tests;

public class IoScoringTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(8)]
    [InlineData(16)]
    public void IoAwardsStarterCountDownToOne(int starters)
    {
        var entries = Enumerable.Range(1, starters).Select(i => new RaceEntry($"Player {i}", i, true)).ToArray();
        var rows = StandingsCalculator.Calculate(new("Lobby", null, [], [new("Track", entries)]), ScoringSystem.IO);
        Assert.Equal(Enumerable.Range(1, starters).Reverse().Select(x => (decimal)x), rows.Select(x => x.Points));
    }

    [Fact]
    public void EachRaceUsesItsOwnStartersIncludingDnfAndExcludingAbsentPlayers()
    {
        var session = new SessionResults("Lobby", null, ["A", "B", "C", "Late joiner"],
        [
            new("Track 1", [new("A", 1, true), new("B", 2, true), new("C", 0, false)]),
            new("Track 2", [new("A", 2, true), new("B", 1, true)])
        ]);
        var rows = StandingsCalculator.Calculate(session, ScoringSystem.IO);
        Assert.Equal(4, rows.Single(x => x.Player == "A").Points);
        Assert.Equal(4, rows.Single(x => x.Player == "B").Points);
        Assert.DoesNotContain(rows, x => x.Player == "C" || x.Player == "Late joiner");
        Assert.Equal("B", rows[0].Player);

        var customPoints = new decimal[ScoringSystem.PlaceCount];
        customPoints[0] = 16;
        customPoints[1] = 15;
        var custom = StandingsCalculator.Calculate(session, new ScoringSystem(customPoints));
        Assert.Equal(31, custom.Single(x => x.Player == "A").Points);
        Assert.Equal(31, custom.Single(x => x.Player == "B").Points);
        Assert.Equal(4, StandingsCalculator.Calculate(session, ScoringSystem.IO)[0].Points);
    }

    [Fact]
    public void IoSettingsRoundTripWithoutBecomingCustom()
    {
        var restored = ScoringSystem.FromJson(ScoringSystem.IO.ToJson());
        Assert.True(restored.IsIo);
        Assert.Equal(3, restored.ForPlace(1, 3));
    }

    [Fact]
    public void UnfinishedRaceDoesNotContributeToIoTotals()
    {
        var session = new SessionResults("Lobby", null, [],
            [new("Track", [new("A", 1, true), new("B", 0, false)], IsComplete: false)]);
        Assert.Empty(StandingsCalculator.Calculate(session, ScoringSystem.IO));
    }
}
