using System.Globalization;
using System.Text.Json;
using RvglLiveScore.Models;

namespace RvglLiveScore.Services;

public static class LobbySettingsParser
{
    public const string Marker = "#Kilabarus Live Standings Settings";

    public static LobbySettingsParseResult Parse(string? description)
    {
        if (string.IsNullOrEmpty(description)) return new(false, null, null);
        var markerIndex = description.IndexOf(Marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0) return new(false, null, null);

        try
        {
            var json = description[(markerIndex + Marker.Length)..].Trim();
            if (json.Length == 0) throw new FormatException("The settings marker must be followed by a JSON object.");
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) throw new FormatException("Lobby settings must be a JSON object.");
            EnsureUniqueProperties(root);

            var locked = OptionalBoolean(root, "IsLocked", false);
            var scoringType = OptionalString(root, "ScoringType", "IO");
            ScoringSystem scoring;
            if (scoringType.Equals("IO", StringComparison.OrdinalIgnoreCase))
            {
                scoring = ScoringSystem.IO;
            }
            else if (scoringType.Equals("Custom", StringComparison.OrdinalIgnoreCase))
            {
                var custom = Required(root, "CustomScoring",
                    "CustomScoring is required when ScoringType is Custom.");
                scoring = ParseCustomScoring(custom);
            }
            else
            {
                throw new FormatException("ScoringType must be IO or Custom.");
            }

            var sessionMode = OptionalString(root, "SessionMode", "Default");
            SessionModeSelection session;
            if (sessionMode.Equals("Default", StringComparison.OrdinalIgnoreCase))
            {
                session = new(SessionMode.Classic, new());
            }
            else if (IsTmMode(sessionMode))
            {
                var tm = Required(root, "TMModeSettings",
                    "TMModeSettings is required when SessionMode is TM.");
                session = new(SessionMode.TmFinalist, ParseTmSettings(tm));
            }
            else if (IsQuickDrawMode(sessionMode))
            {
                session = new(SessionMode.RvQuickDrawTournament2026, new());
            }
            else
            {
                throw new FormatException("SessionMode must be Default, TM Finalist, or RV Quick Draw Tournament 2026.");
            }

            return new(true, new LobbyDisplaySettings(locked, scoring, session), null);
        }
        catch (JsonException)
        {
            return new(true, null, "The lobby settings after the marker are not valid JSON.");
        }
        catch (FormatException exception)
        {
            return new(true, null, exception.Message);
        }
    }

    private static ScoringSystem ParseCustomScoring(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object)
            throw new FormatException("CustomScoring must be an object whose keys are places from 1 to 23.");
        var points = new decimal[ScoringSystem.PlaceCount];
        var assigned = new bool[ScoringSystem.PlaceCount];
        foreach (var property in value.EnumerateObject())
        {
            if (!int.TryParse(property.Name, NumberStyles.None, CultureInfo.InvariantCulture, out var place)
                || place < 1 || place > ScoringSystem.PlaceCount)
                throw new FormatException($"CustomScoring place '{property.Name}' must be between 1 and {ScoringSystem.PlaceCount}.");
            if (assigned[place - 1]) throw new FormatException($"CustomScoring contains place {place} more than once.");
            if (property.Value.ValueKind != JsonValueKind.Number || !property.Value.TryGetDecimal(out var pointValue))
                throw new FormatException($"CustomScoring points for place {place} must be a number.");
            points[place - 1] = pointValue;
            assigned[place - 1] = true;
        }
        return new ScoringSystem(points);
    }

    private static TmFinalistOptions ParseTmSettings(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object) throw new FormatException("TMModeSettings must be an object.");
        var finalistPoints = RequiredInteger(value, "FinalistPoints", 1, 100_000);
        var rounds = OptionalInteger(value, "RoundsPerTrack", 1, 1, 100);
        var warmup = OptionalBoolean(value, "WarmupRound", false);
        var bestLap = OptionalInteger(value, "BestLap", 0, 0, 100_000);
        var podium = OptionalInteger(value, "PodiumInAllRounds", 0, 0, 100_000);
        return new(finalistPoints, rounds, warmup, bestLap > 0, bestLap, podium > 0, podium);
    }

    private static bool IsTmMode(string value) =>
        value.Equals("TM", StringComparison.OrdinalIgnoreCase)
        || value.Equals("TMFinalist", StringComparison.OrdinalIgnoreCase)
        || value.Equals("Finalist", StringComparison.OrdinalIgnoreCase)
        || value.Equals("TMFinalistMode", StringComparison.OrdinalIgnoreCase);

    private static bool IsQuickDrawMode(string value) =>
        value.Equals("RV Quick Draw Tournament 2026", StringComparison.OrdinalIgnoreCase)
        || value.Equals("RVQuickDrawTournament2026", StringComparison.OrdinalIgnoreCase)
        || value.Equals("QuickDraw2026", StringComparison.OrdinalIgnoreCase)
        || value.Equals("QuickDraw", StringComparison.OrdinalIgnoreCase);

    private static JsonElement Required(JsonElement item, string name, string message)
    {
        var value = Find(item, name);
        return value.ValueKind == JsonValueKind.Undefined ? throw new FormatException(message) : value;
    }

    private static string OptionalString(JsonElement item, string name, string fallback)
    {
        var value = Find(item, name);
        if (value.ValueKind == JsonValueKind.Undefined) return fallback;
        if (value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
            throw new FormatException($"{name} must be a string.");
        return value.GetString()!;
    }

    private static bool OptionalBoolean(JsonElement item, string name, bool fallback)
    {
        var value = Find(item, name);
        if (value.ValueKind == JsonValueKind.Undefined) return fallback;
        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => throw new FormatException($"{name} must be true or false.")
        };
    }

    private static int RequiredInteger(JsonElement item, string name, int min, int max)
    {
        var value = Find(item, name);
        if (value.ValueKind == JsonValueKind.Undefined) throw new FormatException($"{name} is required for TM Finalist Mode.");
        return Integer(value, name, min, max);
    }

    private static int OptionalInteger(JsonElement item, string name, int fallback, int min, int max)
    {
        var value = Find(item, name);
        return value.ValueKind == JsonValueKind.Undefined ? fallback : Integer(value, name, min, max);
    }

    private static int Integer(JsonElement value, string name, int min, int max)
    {
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var number) || number < min || number > max)
            throw new FormatException($"{name} must be a whole number between {min} and {max}.");
        return number;
    }

    private static JsonElement Find(JsonElement item, string name)
    {
        foreach (var property in item.EnumerateObject())
            if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) return property.Value;
        return default;
    }

    private static void EnsureUniqueProperties(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in value.EnumerateObject())
            {
                if (!names.Add(property.Name))
                    throw new FormatException($"The field '{property.Name}' appears more than once.");
                EnsureUniqueProperties(property.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in value.EnumerateArray()) EnsureUniqueProperties(item);
        }
    }
}
