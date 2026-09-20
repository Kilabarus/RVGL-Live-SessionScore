using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace RvglLiveScore.Services;

public sealed class CoordinatorSessionMetadataClient(
    IHttpClientFactory clients,
    IOptions<CoordinatorOptions> options,
    ILogger<CoordinatorSessionMetadataClient> logger)
{
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new(StringComparer.Ordinal);

    public async Task<string?> GetDescriptionAsync(string id, CancellationToken cancellationToken)
    {
        var entry = _cache.GetOrAdd(id, _ => new());
        await entry.Gate.WaitAsync(cancellationToken);
        try
        {
            var interval = TimeSpan.FromSeconds(options.Value.PollIntervalSeconds);
            if (entry.HasValue && DateTimeOffset.UtcNow - entry.FetchedAt < interval) return entry.Description;

            using var client = clients.CreateClient("Coordinator");
            var url = options.Value.SessionsUrl.TrimEnd('/') + "/" + Uri.EscapeDataString(id);
            using var response = await client.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                throw new JsonException("Expected a Coordinator session object.");
            entry.Description = Description(document.RootElement);
            entry.FetchedAt = DateTimeOffset.UtcNow;
            entry.HasValue = true;
            return entry.Description;
        }
        finally
        {
            entry.Gate.Release();
        }
    }

    public async Task WatchAsync(string id, Func<string?, Task> onUpdate, CancellationToken cancellationToken)
    {
        var first = true;
        string? previous = null;
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var description = await GetDescriptionAsync(id, cancellationToken);
                if (first || !string.Equals(previous, description, StringComparison.Ordinal))
                {
                    first = false;
                    previous = description;
                    await onUpdate(description);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return; }
            catch (Exception exception) when (exception is HttpRequestException or JsonException or OperationCanceledException)
            {
                logger.LogWarning(exception, "Could not refresh the description for lobby {LobbyId}.", id);
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(options.Value.PollIntervalSeconds), cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return; }
        }
    }

    private static string? Description(JsonElement root)
    {
        foreach (var property in root.EnumerateObject())
            if (property.Name.Equals("Description", StringComparison.OrdinalIgnoreCase))
                return property.Value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(property.Value.GetString())
                    ? property.Value.GetString() : null;
        return null;
    }

    private sealed class CacheEntry
    {
        public SemaphoreSlim Gate { get; } = new(1, 1);
        public bool HasValue { get; set; }
        public DateTimeOffset FetchedAt { get; set; }
        public string? Description { get; set; }
    }
}
