namespace RvglLiveScore.Models;

public enum SessionMode { Classic, TmFinalist, RvQuickDrawTournament2026 }
public sealed record SessionModeSelection(SessionMode Mode, TmFinalistOptions Options);

public sealed record TmFinalistOptions(
    int FinalistPoints = 100,
    int RoundsPerTrack = 3,
    bool WarmupRound = false,
    bool BestLapBonusEnabled = true,
    int BestLapBonusPoints = 32,
    bool PodiumBonusEnabled = false,
    int PodiumBonusPoints = 16);

public enum TmFinalistStatus { Racing, Finalist, Winner }

public sealed record TmStanding(
    int Position,
    string Player,
    decimal Points,
    TmFinalistStatus Status,
    IReadOnlyList<decimal> LastRoundParts,
    int? BestLapMilliseconds,
    bool PodiumEligible);
