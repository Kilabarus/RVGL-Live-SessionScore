using System.Net;
using System.Net.ServerSentEvents;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RvglLiveScore.Models;

namespace RvglLiveScore.Services;

public sealed class CoordinatorSessionClient(
    IHttpClientFactory clients, IOptions<CoordinatorOptions> options, ILogger<CoordinatorSessionClient> logger)
{
    // The page owns this subscription and cancels it when the visitor navigates away.
    public async Task WatchAsync(string id, Func<SessionUpdate, Task> onUpdate, CancellationToken cancellationToken)
    {
        SessionResults? results = null;
        var retrySeconds = 2;
        var url = options.Value.SessionsUrl.TrimEnd('/') + "/" + Uri.EscapeDataString(id) + "/events";
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var client = clients.CreateClient("CoordinatorStream");
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                using var headerTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                headerTimeout.CancelAfter(TimeSpan.FromSeconds(15));
                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, headerTimeout.Token);
                headerTimeout.CancelAfter(Timeout.InfiniteTimeSpan);
                if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
                {
                    await onUpdate(new(results, results is null ? SessionConnection.Unavailable : SessionConnection.Ended));
                    return;
                }
                if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)
                {
                    await onUpdate(new(null, SessionConnection.Unavailable));
                    return;
                }
                response.EnsureSuccessStatusCode();
                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                await foreach (var item in SseParser.Create(stream).EnumerateAsync(cancellationToken))
                {
                    if (item.EventType == "end")
                    {
                        await onUpdate(new(results, SessionConnection.Ended));
                        return;
                    }
                    if (item.EventType != "message" || string.IsNullOrWhiteSpace(item.Data)) continue;
                    results = SessionResultsParser.Parse(item.Data, results);
                    retrySeconds = 2;
                    await onUpdate(new(results, SessionConnection.Live));
                }
                // EOF without an explicit end event is a lost connection, not a finished session.
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return; }
            catch (UnauthorizedAccessException)
            {
                await onUpdate(new(null, SessionConnection.Unavailable));
                return;
            }
            catch (Exception exception) when (exception is HttpRequestException or IOException or JsonException or OperationCanceledException)
            {
                logger.LogWarning(exception, "Could not read results for lobby {LobbyId}.", id);
            }
            await onUpdate(new(results, SessionConnection.Reconnecting));
            try { await Task.Delay(TimeSpan.FromSeconds(retrySeconds), cancellationToken); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return; }
            retrySeconds = Math.Min(retrySeconds * 2, 30);
        }
    }
}
