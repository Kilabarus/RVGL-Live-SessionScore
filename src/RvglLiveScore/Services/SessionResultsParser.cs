using System.Text.Json;
using RvglLiveScore.Models;

namespace RvglLiveScore.Services;

public static class SessionResultsParser
{
    public static SessionResults Parse(string json, SessionResults? previous = null)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object) throw new JsonException("Expected a session object.");
        if (Get(root, "is_private", "private").ValueKind == JsonValueKind.True
            || Get(root, "is_public", "public").ValueKind == JsonValueKind.False)
            throw new UnauthorizedAccessException("The session is private.");

        var name = Text(Get(root, "Name")) ?? previous?.Name ?? "Lobby";
        var game = Get(root, "GameData", "game_data");
        var track = Text(Get(game, "TrackName", "track_name", "Track"))
            ?? Text(Get(root, "TrackName", "current_track"));
        if (game.ValueKind == JsonValueKind.Undefined && Get(root, "TrackName", "current_track").ValueKind == JsonValueKind.Undefined)
            track = previous?.CurrentTrack;
        // A missing history is a metadata-only update. An explicit empty history resets the standings.
        var history = Get(root, "History");
        IReadOnlyList<RaceResult> races = previous?.Races ?? [];
        if (history.ValueKind != JsonValueKind.Undefined)
        {
            var parsed = new List<RaceResult>();
            foreach (var race in Array(history))
            {
                if (race.ValueKind != JsonValueKind.Object) throw new JsonException("Invalid race.");
                var entries = new List<RaceEntry>();
                foreach (var player in Array(Get(race, "Entries", "Results")))
                {
                    var playerName = Text(Get(player, "Name")) ?? throw new JsonException("Missing player name.");
                    var positionValue = Get(player, "Position");
                    var position = positionValue.ValueKind == JsonValueKind.Number && positionValue.TryGetInt32(out var number)
                        ? number : 0;
                    var finishedValue = Get(player, "Finished");
                    var finished = finishedValue.ValueKind == JsonValueKind.True
                        || string.Equals(Text(finishedValue), "true", StringComparison.OrdinalIgnoreCase);
                    var bestLap = LapMilliseconds(Get(player, "BestLap", "BestLapTime", "BestLapMs", "best_lap_str"));
                    entries.Add(new RaceEntry(playerName, position, finished && position > 0, bestLap));
                }
                var finishedAt = Get(race, "FinishedAt", "finished_at");
                // Live Coordinator history includes the current race before it has finished.
                // Older snapshots without timestamps contain only finalized results.
                var complete = Get(race, "StartedAt", "started_at").ValueKind == JsonValueKind.Undefined
                    || (finishedAt.ValueKind == JsonValueKind.Number && finishedAt.TryGetInt64(out var timestamp) && timestamp > 0);
                parsed.Add(new RaceResult(Text(Get(race, "TrackName", "track")) ?? "Unknown track", entries, complete));
            }
            races = parsed;
        }
        var roster = Get(root, "Peers", "Players");
        var players = roster.ValueKind == JsonValueKind.Undefined
            ? previous?.Players ?? []
            : Array(roster).Where(x => Get(x, "Spectating").ValueKind != JsonValueKind.True)
                .Select(x => Text(x) ?? Text(Get(x, "Name")))
                .Where(x => x is not null).Cast<string>().Distinct(StringComparer.Ordinal).ToArray();
        var queue = Get(root, "Queue");
        int? remainingRaces = queue.ValueKind == JsonValueKind.Array ? queue.GetArrayLength() : null;
        var nextTrack = remainingRaces > 0
            ? Text(Get(queue[0], "TrackName", "track_name", "Track")) ?? Text(queue[0])
            : null;
        var descriptionValue = Get(root, "Description");
        var description = descriptionValue.ValueKind == JsonValueKind.Undefined
            ? previous?.Description : Text(descriptionValue);
        return new SessionResults(name, track, players, races, remainingRaces, nextTrack, description);
    }

    private static IEnumerable<JsonElement> Array(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Array => value.EnumerateArray(),
        JsonValueKind.Null => [],
        _ => throw new JsonException("Expected an array.")
    };

    private static string? Text(JsonElement value) => value.ValueKind == JsonValueKind.String
        && !string.IsNullOrWhiteSpace(value.GetString()) ? value.GetString() : null;

    private static int? LapMilliseconds(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Number)
            return value.TryGetInt32(out var number) && number > 0 ? number : null;
        var parts = Text(value)?.Split(':');
        if (parts is not { Length: 3 }
            || !int.TryParse(parts[0], out var minutes) || minutes < 0
            || !int.TryParse(parts[1], out var seconds) || seconds is < 0 or > 59
            || !int.TryParse(parts[2], out var milliseconds) || milliseconds is < 0 or > 999)
            return null;
        var total = (long)minutes * 60000 + seconds * 1000 + milliseconds;
        return total is > 0 and <= int.MaxValue ? (int)total : null;
    }

    private static JsonElement Get(JsonElement item, params string[] names)
    {
        if (item.ValueKind != JsonValueKind.Object) return default;
        foreach (var name in names)
            foreach (var property in item.EnumerateObject())
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase)) return property.Value;
        return default;
    }
}
