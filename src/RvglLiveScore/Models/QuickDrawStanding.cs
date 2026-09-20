namespace RvglLiveScore.Models;

public sealed record QuickDrawStanding(
    int Position,
    string Player,
    IReadOnlyList<decimal?> RacePoints,
    decimal Points,
    decimal? Diff,
    decimal? DiffLeader);
