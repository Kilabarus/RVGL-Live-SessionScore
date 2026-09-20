using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RvglLiveScore.Services;

namespace RvglLiveScore.Tests;

public class CoordinatorSessionMetadataClientTests
{
    [Fact]
    public async Task ReadsDescriptionCaseInsensitivelyAndCachesPerLobby()
    {
        using var clients = new FakeClients("{\"description\":\"Settings text\"}");
        var service = Client(clients);
        var values = await Task.WhenAll(
            service.GetDescriptionAsync("lobby id", CancellationToken.None),
            service.GetDescriptionAsync("lobby id", CancellationToken.None));
        Assert.All(values, value => Assert.Equal("Settings text", value));
        Assert.Equal(1, clients.Calls);
        Assert.EndsWith("/api/sessions/lobby id", clients.LastUrl);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"Description\":null}")]
    [InlineData("{\"Description\":\"  \"}")]
    public async Task MissingOrEmptyDescriptionReturnsNull(string json)
    {
        using var clients = new FakeClients(json);
        Assert.Null(await Client(clients).GetDescriptionAsync("lobby", CancellationToken.None));
    }

    private static CoordinatorSessionMetadataClient Client(IHttpClientFactory clients) => new(
        clients,
        Options.Create(new CoordinatorOptions
        {
            SessionsUrl = "https://coordinator.example/api/sessions",
            PollIntervalSeconds = 10
        }),
        NullLogger<CoordinatorSessionMetadataClient>.Instance);

    private sealed class FakeClients(string json) : HttpMessageHandler, IHttpClientFactory
    {
        public int Calls { get; private set; }
        public string? LastUrl { get; private set; }
        public HttpClient CreateClient(string name) => new(this, disposeHandler: false);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            LastUrl = request.RequestUri!.ToString();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }
}
