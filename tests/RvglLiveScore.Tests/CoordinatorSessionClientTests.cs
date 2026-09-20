using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RvglLiveScore.Models;
using RvglLiveScore.Services;

namespace RvglLiveScore.Tests;

public class CoordinatorSessionClientTests
{
    [Fact]
    public async Task ReadsMultilineSseAndRetainsResultsOnExplicitEnd()
    {
        var data = ": heartbeat\n\ndata: " + SessionResultsParserTests.Snapshot.Replace("\n", "\ndata: ")
            + "\n\nevent: end\ndata: {}\n\n";
        using var factory = new FakeClients(_ => Response(data));
        var updates = new List<SessionUpdate>();
        await Client(factory).WatchAsync("lobby-id", update => { updates.Add(update); return Task.CompletedTask; }, CancellationToken.None);
        Assert.Equal([SessionConnection.Live, SessionConnection.Ended], updates.Select(x => x.Connection));
        Assert.Same(updates[0].Results, updates[1].Results);
        Assert.EndsWith("/api/sessions/lobby-id/events", factory.LastUrl);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task MissingAndPrivateSessionsStopWithoutRetrying(HttpStatusCode status)
    {
        using var factory = new FakeClients(_ => new HttpResponseMessage(status));
        SessionUpdate? last = null;
        await Client(factory).WatchAsync("missing", update => { last = update; return Task.CompletedTask; }, CancellationToken.None);
        Assert.Equal(SessionConnection.Unavailable, last!.Connection);
        Assert.Null(last.Results);
        Assert.Equal(1, factory.Calls);
    }

    [Fact]
    public async Task UnexpectedEofRetriesAndReplacesSnapshotWithoutDoublingPoints()
    {
        var frame = "data: " + SessionResultsParserTests.Snapshot.Replace("\n", "\ndata: ") + "\n\n";
        using var factory = new FakeClients(call => Response(frame + (call > 1 ? "event: end\ndata: {}\n\n" : "")));
        var updates = new List<SessionUpdate>();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await Client(factory).WatchAsync("lobby", update => { updates.Add(update); return Task.CompletedTask; }, deadline.Token);
        Assert.Contains(updates, x => x.Connection == SessionConnection.Reconnecting);
        Assert.Equal(2, factory.Calls);
        Assert.Equal(SessionConnection.Ended, updates[^1].Connection);
        var fixedPoints = new ScoringSystem(Enumerable.Repeat(16m, ScoringSystem.PlaceCount));
        Assert.Equal(16, StandingsCalculator.Calculate(updates[^1].Results!, fixedPoints)[0].Points);
    }

    [Fact]
    public async Task CancellationStopsRetries()
    {
        using var factory = new FakeClients(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        using var cancel = new CancellationTokenSource();
        await Client(factory).WatchAsync("lobby", update => { cancel.Cancel(); return Task.CompletedTask; }, cancel.Token);
        Assert.Equal(1, factory.Calls);
    }

    private static HttpResponseMessage Response(string text) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(text, Encoding.UTF8, "text/event-stream")
    };
    private static CoordinatorSessionClient Client(IHttpClientFactory factory) => new(factory,
        Options.Create(new CoordinatorOptions { SessionsUrl = "https://coordinator.example/api/sessions" }),
        NullLogger<CoordinatorSessionClient>.Instance);

    private sealed class FakeClients(Func<int, HttpResponseMessage> respond) : HttpMessageHandler, IHttpClientFactory
    {
        public int Calls { get; private set; }
        public string? LastUrl { get; private set; }
        public HttpClient CreateClient(string name) => new(this, disposeHandler: false);
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastUrl = request.RequestUri!.ToString();
            return Task.FromResult(respond(++Calls));
        }
    }
}
