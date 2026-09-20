namespace RvglLiveScore.Models;

public sealed record RaceEntry(string Name, int Position, bool Finished, int? BestLapMilliseconds = null);
public sealed record RaceResult(string Track, IReadOnlyList<RaceEntry> Entries, bool IsComplete = true);
public sealed record SessionResults(
    string Name,
    string? CurrentTrack,
    IReadOnlyList<string> Players,
    IReadOnlyList<RaceResult> Races,
    int? RemainingRaces = null,
    string? NextTrack = null,
    string? Description = null);

public enum SessionConnection { Loading, Live, Reconnecting, Ended, Unavailable }
public sealed record SessionUpdate(SessionResults? Results, SessionConnection Connection);

public sealed record Standing(
    int Position, string Player, decimal Points, decimal LastRoundPoints,
    int Wins, decimal? Diff, decimal? DiffLeader);
