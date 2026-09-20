using System.Text.Json;
using RvglLiveScore.Models;
using RvglLiveScore.Services;

namespace RvglLiveScore.Tests;

public class SessionResultsParserTests
{
    public const string Snapshot = """
        {"Name":"GOON'S RACING","Public":true,"GameData":{"TrackName":"Toys in the Hood 1"},
         "Players":[{"Name":"Aqua"},{"Name":"Abbe"}],
         "History":[{"TrackName":"Museum 1","Entries":[
             {"Name":"Aqua","Position":1,"Finished":true},
             {"Name":"Abbe","Position":2,"Finished":true}]}]}
        """;

    [Fact]
    public void ParsesCoordinatorHistoryAndTrack()
    {
        var data = SessionResultsParser.Parse(Snapshot);
        Assert.Equal("GOON'S RACING", data.Name);
        Assert.Equal("Toys in the Hood 1", data.CurrentTrack);
        Assert.Equal(2, data.Players.Count);
        var race = Assert.Single(data.Races);
        Assert.Equal("Museum 1", race.Track);
        Assert.Equal(new RaceEntry("Aqua", 1, true), race.Entries[0]);
    }

    [Fact]
    public void ParsesBestLapTimesAndIgnoresInvalidValues()
    {
        var data = SessionResultsParser.Parse("""
            {"History":[{"TrackName":"Track","Entries":[
                {"Name":"A","Position":1,"Finished":true,"BestLap":"00:43:125"},
                {"Name":"B","Position":2,"Finished":true,"BestLapMs":45123},
                {"Name":"C","Position":3,"Finished":true,"BestLap":"00:72:999"}]}]}
            """);
        Assert.Equal(43125, data.Races[0].Entries[0].BestLapMilliseconds);
        Assert.Equal(45123, data.Races[0].Entries[1].BestLapMilliseconds);
        Assert.Null(data.Races[0].Entries[2].BestLapMilliseconds);
    }

    [Fact]
    public void QueueCountFollowsLiveSnapshotsAndClearsWhenUnavailable()
    {
        var queued = SessionResultsParser.Parse("""
            {"Name":"test","Queue":[{"TrackName":"Track 1"},{"TrackName":"Track 2"},
                                     {"TrackName":"Track 3"},{"TrackName":"Track 4"}]}
            """);
        Assert.Equal(4, queued.RemainingRaces);
        Assert.Equal("Track 1", queued.NextTrack);
        foreach (var snapshot in new[] { "{\"Name\":\"test\"}", "{\"Queue\":null}", "{\"Queue\":{}}" })
        {
            var unavailable = SessionResultsParser.Parse(snapshot, queued);
            Assert.Null(unavailable.RemainingRaces);
            Assert.Null(unavailable.NextTrack);
        }
        var empty = SessionResultsParser.Parse("{\"Queue\":[]}", queued);
        Assert.Equal(0, empty.RemainingRaces);
        Assert.Null(empty.NextTrack);
        var updated = SessionResultsParser.Parse("{\"Queue\":[{\"TrackName\":\"Track 2\"}]}", queued);
        Assert.Equal(1, updated.RemainingRaces);
        Assert.Equal("Track 2", updated.NextTrack);
        Assert.Null(SessionResultsParser.Parse("{\"Queue\":[{}]}", updated).NextTrack);
    }

    [Fact]
    public void RejoinedPlayerCanAppearTwiceInOneRace()
    {
        var session = SessionResultsParser.Parse("""
            {"Name":"CASUAL PROS RVC","History":[{"TrackName":"Track","Entries":[
              {"PlayerID":101,"Name":"Winner","Position":1,"Finished":true},
              {"PlayerID":110,"Name":"ROG","Position":2,"Finished":true},
              {"PlayerID":103,"Name":"ROG","Position":0,"Finished":false}]}]}
            """);
        Assert.Equal(3, Assert.Single(session.Races).Entries.Count);
        var rows = StandingsCalculator.Calculate(session, ScoringSystem.IO);
        Assert.Equal(2, rows.Count);
        Assert.Equal(3, rows.Single(x => x.Player == "Winner").Points);
        var rejoined = rows.Single(x => x.Player == "ROG");
        Assert.Equal(2, rejoined.Points);
        Assert.Equal(2, rejoined.LastRoundPoints);
    }

    [Fact]
    public void SnapshotsReplaceHistoryInsteadOfAppendingAndEmptyHistoryResets()
    {
        var first = SessionResultsParser.Parse(Snapshot);
        var repeated = SessionResultsParser.Parse(Snapshot, first);
        Assert.Single(repeated.Races);
        var metadataOnly = SessionResultsParser.Parse("{\"Name\":\"Renamed\"}", repeated);
        Assert.Single(metadataOnly.Races);
        Assert.Equal("Renamed", metadataOnly.Name);
        Assert.Equal(first.CurrentTrack, metadataOnly.CurrentTrack);
        Assert.Empty(SessionResultsParser.Parse("{\"History\":[]}", repeated).Races);
    }

    [Fact]
    public void SupportsLowercaseFieldsAndUnfinishedResults()
    {
        var session = SessionResultsParser.Parse("""
            {"name":"Race","history":[{"track":"Track","results":[
              {"name":"A","position":0,"finished":true},
              {"name":"B","position":2,"finished":"true"},
              {"name":"C","position":1,"finished":false}]}]}
            """);
        Assert.False(session.Races[0].Entries[0].Finished);
        Assert.True(session.Races[0].Entries[1].Finished);
        Assert.False(session.Races[0].Entries[2].Finished);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("{\"History\":{}}")]
    [InlineData("{\"History\":[{\"Entries\":[{}]}]}")]
    public void InvalidSnapshotsAreRejected(string json) => Assert.Throws<JsonException>(() => SessionResultsParser.Parse(json));

    [Fact]
    public void PrivateSessionIsNotDisplayed() => Assert.Throws<UnauthorizedAccessException>(() =>
        SessionResultsParser.Parse("{\"public\":false,\"History\":[]}"));

    [Fact]
    public void CoordinatorPeersAppearBeforeFirstRaceAndSpectatorsAreExcluded()
    {
        var data = SessionResultsParser.Parse("""
            {"Name":"test","Public":true,"TrackName":"Toys in the Hood 1","History":[],
             "Peers":[{"Name":"KILABARUS","Spectating":false},{"Name":"Spectator","Spectating":true}]}
            """);
        Assert.Equal("KILABARUS", Assert.Single(data.Players));
        Assert.Equal("Toys in the Hood 1", data.CurrentTrack);
        Assert.Empty(StandingsCalculator.Calculate(data, ScoringSystem.CustomDefault));
    }

    [Fact]
    public void CurrentRaceDoesNotAwardPointsUntilCoordinatorMarksItFinished()
    {
        const string unfinished = """
            {"Name":"test","Public":true,"TrackName":"SuperMarket 2","History":[
              {"StartedAt":1000,"FinishedAt":2000,"Entries":[{"Name":"A","Position":1,"Finished":true}]},
              {"StartedAt":3000,"Entries":[{"Name":"A","Position":1,"Finished":true}]}]}
            """;
        var duringRace = SessionResultsParser.Parse(unfinished);
        Assert.False(duringRace.Races[1].IsComplete);
        var fixedPoints = new ScoringSystem(Enumerable.Repeat(16m, ScoringSystem.PlaceCount));
        var first = Assert.Single(StandingsCalculator.Calculate(duringRace, fixedPoints));
        Assert.Equal(16, first.Points);
        Assert.Equal(16, first.LastRoundPoints);
        Assert.Equal(1, first.Wins);

        var finished = SessionResultsParser.Parse(unfinished.Replace("\"StartedAt\":3000", "\"StartedAt\":3000,\"FinishedAt\":4000"), duringRace);
        var second = Assert.Single(StandingsCalculator.Calculate(finished, fixedPoints));
        Assert.Equal(32, second.Points);
        Assert.Equal(2, second.Wins);
    }
}
