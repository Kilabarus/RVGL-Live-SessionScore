using RvglLiveScore.Models;

namespace RvglLiveScore.Tests;

public class ScoringSystemTests
{
    [Fact]
    public void CustomStartsWithTwentyThreeZeroes()
    {
        Assert.Equal(23, ScoringSystem.PlaceCount);
        Assert.Equal(23, ScoringSystem.CustomDefault.Points.Count);
        Assert.All(ScoringSystem.CustomDefault.Points, points => Assert.Equal(0, points));
        Assert.Equal(0, ScoringSystem.CustomDefault.ForPlace(23, 23));
    }

    [Fact]
    public void ExportImportRoundTripPreservesEveryPlace()
    {
        var values = Enumerable.Range(1, ScoringSystem.PlaceCount).Select(x => (decimal)x - 3.75m).ToArray();
        var scoring = new ScoringSystem(values);
        var imported = ScoringSystem.FromJson(scoring.ToJson());
        Assert.Equal(values, imported.Points);
        values[0] = 1234;
        Assert.Equal(-2.75m, scoring.ForPlace(1, 23));
        Assert.Equal(19.25m, scoring.ForPlace(23, 23));
        Assert.Equal(0, scoring.ForPlace(24, 24));
    }

    [Fact]
    public void PlainArrayCanBeImported()
    {
        var values = Enumerable.Range(0, ScoringSystem.PlaceCount).Select(x => (decimal)x).ToArray();
        var scoring = ScoringSystem.FromJson($"[{string.Join(",", values)}]");
        Assert.False(scoring.IsIo);
        Assert.Equal(values, scoring.Points);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("[1,2,3]")]
    [InlineData("{\"version\":\"1\",\"points\":[]}")]
    [InlineData("{\"version\":2,\"points\":[]}")]
    [InlineData("{\"version\":2,\"mode\":\"io\"}")]
    [InlineData("{\"version\":3,\"mode\":\"custom\",\"points\":[]}")]
    [InlineData("[16,15,14,13,12,11,10,9,8,7,6,5,4,3,2,1]")]
    [InlineData("[16,15,14,13,12,11,10,9,8,7,6,5,4,3,2,\"one\"]")]
    public void InvalidFilesAreRejected(string json) => Assert.Throws<FormatException>(() => ScoringSystem.FromJson(json));

    [Theory]
    [InlineData(1000001)]
    [InlineData(-1000001)]
    [InlineData(0.001)]
    public void InvalidPointsAreRejected(decimal value)
    {
        var values = ScoringSystem.CustomDefault.Points.ToArray();
        values[0] = value;
        Assert.Throws<FormatException>(() => new ScoringSystem(values));
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("1,5")]
    [InlineData("NaN")]
    public void InvalidFieldsDoNotSilentlyBecomeZero(string text)
    {
        var fields = ScoringSystem.CustomDefault.Points.Select(ScoringSystem.Format).ToArray();
        fields[5] = text;
        Assert.Throws<FormatException>(() => ScoringSystem.FromFields(fields));
    }
}
