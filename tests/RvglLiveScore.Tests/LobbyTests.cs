using System.Text.Json;
using RvglLiveScore.Models;
using RvglLiveScore.Services;

namespace RvglLiveScore.Tests;

public class LobbyTests
{
    private static IReadOnlyList<Lobby> Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return LobbyParser.Parse(document.RootElement);
    }

    [Fact]
    public void CoordinatorNameAndExplicitZeroTakePrecedence()
    {
        var lobby = Assert.Single(Parse("""
            [{"id":"test-id","name":"Test","host":"eu.rv.gl","public":true,
              "player_count":0,"players":["old player"]}]
            """));
        Assert.Equal(new Lobby("test-id", "Test", 0), lobby);
    }

    [Fact]
    public void LobbiesAreOrderedByNumericCountThenName()
    {
        var lobbies = Parse("""
            [{"name":"Small","player_count":2},{"name":"Zulu","player_count":10},
             {"name":"Full","player_count":16},{"name":"Alpha","player_count":10}]
            """);
        Assert.Equal(["Full", "Alpha", "Zulu", "Small"], lobbies.Select(x => x.Name));
    }

    [Fact]
    public void PrivateSessionsAreExcluded()
    {
        var lobbies = Parse("""
            [{"name":"Hidden","public":false},{"name":"Private","is_private":true},
             {"name":"Open","public":true}]
            """);
        Assert.Equal("Open", Assert.Single(lobbies).Name);
    }

    [Fact]
    public void LegacyCasingAndPlayerArrayAreSupported()
    {
        var lobby = Assert.Single(Parse("""
            [{"LobbyID":"legacy","Name":"Race","Players":["A","B"]}]
            """));
        Assert.Equal(new Lobby("legacy", "Race", 2), lobby);
    }

    [Fact]
    public void EmptyResponseIsAValidEmptyList() => Assert.Empty(Parse("[]"));

    [Theory]
    [InlineData("{}")]
    [InlineData("[null]")]
    public void UnexpectedPayloadIsAnError(string json) =>
        Assert.Throws<JsonException>(() => Parse(json));

    [Fact]
    public void FailureClearsStaleLobbiesAndSuccessRecovers()
    {
        var directory = new LobbyDirectory();
        Assert.True(directory.Snapshot.IsLoading);
        directory.Update([new Lobby("1", "Race", 16)]);
        var lastSuccess = directory.Snapshot.LastUpdated;
        directory.MarkUnavailable();
        Assert.Empty(directory.Snapshot.Lobbies);
        Assert.NotNull(directory.Snapshot.Error);
        Assert.False(directory.Snapshot.IsLoading);
        Assert.Equal(lastSuccess, directory.Snapshot.LastUpdated);
        directory.Update([new Lobby("2", "New race", 1)]);
        Assert.Null(directory.Snapshot.Error);
        Assert.Equal("New race", Assert.Single(directory.Snapshot.Lobbies).Name);
    }
}
