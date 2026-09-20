using RvglLiveScore.Models;
using RvglLiveScore.Services;

namespace RvglLiveScore.Tests;

public class LobbySettingsParserTests
{
    private static LobbySettingsParseResult Parse(string json, string prefix = "Lobby description\n") =>
        LobbySettingsParser.Parse(prefix + LobbySettingsParser.Marker + "\n" + json);

    [Fact]
    public void MissingMarkerLeavesLobbySettingsAlone()
    {
        var result = LobbySettingsParser.Parse("Ordinary lobby description");
        Assert.False(result.HasMarker);
        Assert.Null(result.Settings);
        Assert.Null(result.Error);
    }

    [Fact]
    public void EmptyObjectUsesDocumentedDefaults()
    {
        var result = Parse("{}");
        var settings = Assert.IsType<LobbyDisplaySettings>(result.Settings);
        Assert.False(settings.IsLocked);
        Assert.True(settings.Scoring.IsIo);
        Assert.Equal(SessionMode.Classic, settings.Session.Mode);
        Assert.Equal(new TmFinalistOptions(), settings.Session.Options);
        Assert.Null(result.Error);
    }

    [Fact]
    public void ParsesFullSettingsWithoutDependingOnPropertyOrderOrCase()
    {
        var result = Parse("""
            {
              "tmmodesettings": {
                "podiuminallrounds": 16,
                "bestlap": 32,
                "warmupround": true,
                "roundspertrack": 3,
                "finalistpoints": 100
              },
              "SESSIONMODE": "tmfinalistmode",
              "customscoring": { "16": 1, "2": 15.5, "1": 16, "23": -2 },
              "scoringtype": "custom",
              "islocked": true
            }
            """);
        var settings = Assert.IsType<LobbyDisplaySettings>(result.Settings);
        Assert.True(settings.IsLocked);
        Assert.False(settings.Scoring.IsIo);
        Assert.Equal(16, settings.Scoring.Points[0]);
        Assert.Equal(15.5m, settings.Scoring.Points[1]);
        Assert.Equal(0, settings.Scoring.Points[2]);
        Assert.Equal(1, settings.Scoring.Points[15]);
        Assert.Equal(-2, settings.Scoring.Points[22]);
        Assert.Equal(SessionMode.TmFinalist, settings.Session.Mode);
        Assert.Equal(new TmFinalistOptions(100, 3, true, true, 32, true, 16), settings.Session.Options);
    }

    [Theory]
    [InlineData("TM")]
    [InlineData("TMFinalist")]
    [InlineData("Finalist")]
    [InlineData("TMFinalistMode")]
    [InlineData("tmfinalistmode")]
    public void SupportsEveryTmAlias(string alias)
    {
        var result = Parse($"{{\"SessionMode\":\"{alias}\",\"TMModeSettings\":{{\"FinalistPoints\":120}}}}");
        var settings = Assert.IsType<LobbyDisplaySettings>(result.Settings);
        Assert.Equal(SessionMode.TmFinalist, settings.Session.Mode);
        Assert.Equal(new TmFinalistOptions(120, 1, false, false, 0, false, 0), settings.Session.Options);
    }

    [Theory]
    [InlineData("{not json}")]
    [InlineData("{\"ScoringType\":\"Custom\"}")]
    [InlineData("{\"SessionMode\":\"TM\"}")]
    [InlineData("{\"SessionMode\":\"TM\",\"TMModeSettings\":{}}")]
    [InlineData("{\"IsLocked\":true,\"islocked\":false}")]
    [InlineData("{\"ScoringType\":\"Custom\",\"CustomScoring\":{\"24\":1}}")]
    public void InvalidSettingsReturnDismissibleWarningData(string json)
    {
        var result = Parse(json);
        Assert.True(result.HasMarker);
        Assert.Null(result.Settings);
        Assert.NotEmpty(Assert.IsType<string>(result.Error));
    }
}
