using System.Text.Json;
using Microsoft.Extensions.Options;

namespace RvglLiveScore.Services;

public sealed class CoordinatorWorker(
    IHttpClientFactory clients,
    LobbyDirectory directory,
    IOptions<CoordinatorOptions> options,
    ILogger<CoordinatorWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // One request per interval for the whole app, regardless of visitor count.
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var client = clients.CreateClient("Coordinator");
                using var response = await client.GetAsync(options.Value.SessionsUrl, stoppingToken);
                response.EnsureSuccessStatusCode();
                await using var stream = await response.Content.ReadAsStreamAsync(stoppingToken);
                using var payload = await JsonDocument.ParseAsync(stream, cancellationToken: stoppingToken);
                directory.Update(LobbyParser.Parse(payload.RootElement));
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception) when (exception is HttpRequestException or JsonException or OperationCanceledException)
            {
                directory.MarkUnavailable();
                logger.LogWarning(exception, "Could not refresh the Coordinator lobby list.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(options.Value.PollIntervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
