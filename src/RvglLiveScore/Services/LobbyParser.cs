using System.Text.Json;
using RvglLiveScore.Models;

namespace RvglLiveScore.Services;

public static class LobbyParser
{
    // Accept the Coordinator fields used by the previous client, including casing variants.
    public static IReadOnlyList<Lobby> Parse(JsonElement payload)
    {
        if (payload.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("Expected a JSON array of Coordinator sessions.");
        }

        var lobbies = new List<Lobby>();
        foreach (var session in payload.EnumerateArray())
        {
            if (session.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException("Expected a Coordinator session object.");
            }

            if (Get(session, "is_private", "private").ValueKind == JsonValueKind.True
                || Get(session, "is_public", "public").ValueKind == JsonValueKind.False)
            {
                continue;
            }

            var name = Text(Get(session, "name")) ?? Text(Get(session, "host")) ?? "Public lobby";
            var id = Text(Get(session, "id", "lobby_id", "coord_id", "LobbyID")) ?? name;
            var count = Get(session, "player_count", "PlayerCount");
            var players = Get(session, "players", "results");
            var playerCount = count.ValueKind == JsonValueKind.Number && count.TryGetInt32(out var number)
                ? Math.Max(0, number)
                : players.ValueKind == JsonValueKind.Array ? players.GetArrayLength() : 0;
            lobbies.Add(new Lobby(id, name, playerCount));
        }

        return lobbies.OrderByDescending(lobby => lobby.PlayerCount)
            .ThenBy(lobby => lobby.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string? Text(JsonElement value) =>
        value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString() : null;

    private static JsonElement Get(JsonElement item, params string[] names)
    {
        foreach (var name in names)
        {
            foreach (var property in item.EnumerateObject())
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return property.Value;
                }
            }
        }
        return default;
    }
}
