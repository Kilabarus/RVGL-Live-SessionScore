using System.Globalization;
using System.Text.Json;

namespace RvglLiveScore.Models;

public sealed class ScoringSystem
{
    public const int PlaceCount = 23;
    public const decimal MaxPoints = 1_000_000;
    public const int MaxFileBytes = 16 * 1024;
    public static ScoringSystem IO { get; } = new();
    public static ScoringSystem CustomDefault { get; } = new(new decimal[PlaceCount]);
    public IReadOnlyList<decimal> Points { get; }
    public bool IsIo { get; }

    private ScoringSystem()
    {
        IsIo = true;
        Points = Array.Empty<decimal>();
    }

    public ScoringSystem(IEnumerable<decimal> points)
    {
        var values = points.ToArray();
        if (values.Length != PlaceCount)
            throw new FormatException($"Provide points for all {PlaceCount} places.");
        if (values.Any(x => Math.Abs(x) > MaxPoints || decimal.Round(x, 2) != x))
            throw new FormatException("Use numbers between -1,000,000 and 1,000,000, with up to 2 decimal places.");
        Points = Array.AsReadOnly(values);
    }

    public decimal ForPlace(int place, int starters)
    {
        if (place < 1) return 0;
        return IsIo ? Math.Max(0, starters - place + 1) : place <= PlaceCount ? Points[place - 1] : 0;
    }
    public static string Format(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    public static ScoringSystem FromFields(IEnumerable<string> fields)
    {
        var values = fields.Select((text, index) =>
        {
            if (!decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out var value))
                throw new FormatException($"Enter a valid number for place {index + 1} (use a dot for decimals).");
            return value;
        });
        return new ScoringSystem(values);
    }

    public string ToJson() => IsIo
        ? JsonSerializer.Serialize(new { version = 3, mode = "io" })
        : JsonSerializer.Serialize(new { version = 3, mode = "custom", points = Points },
            new JsonSerializerOptions { WriteIndented = true });

    public static ScoringSystem FromJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var points = root;
            if (root.ValueKind == JsonValueKind.Object)
            {
                if (!root.TryGetProperty("version", out var versionElement) || versionElement.ValueKind != JsonValueKind.Number
                    || !versionElement.TryGetInt32(out var version) || version != 3)
                    throw new FormatException("Unsupported scoring file version.");
                if (!root.TryGetProperty("mode", out var mode) || mode.ValueKind != JsonValueKind.String)
                    throw new FormatException("Expected an IO or custom scoring mode.");
                if (mode.GetString() == "io") return IO;
                if (mode.GetString() != "custom") throw new FormatException("Unknown scoring mode.");
                if (!root.TryGetProperty("points", out points))
                    throw new FormatException("Expected an array of point values.");
            }
            if (points.ValueKind != JsonValueKind.Array)
                throw new FormatException("Expected an array of point values.");
            return new ScoringSystem(points.EnumerateArray().Select(x =>
                x.ValueKind == JsonValueKind.Number && x.TryGetDecimal(out var value)
                    ? value : throw new FormatException("Every point value must be a number.")));
        }
        catch (JsonException)
        {
            throw new FormatException("This file is not valid JSON.");
        }
    }
}
