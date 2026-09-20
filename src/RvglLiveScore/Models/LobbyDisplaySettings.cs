namespace RvglLiveScore.Models;

public sealed record LobbyDisplaySettings(
    bool IsLocked,
    ScoringSystem Scoring,
    SessionModeSelection Session);

public sealed record LobbySettingsParseResult(
    bool HasMarker,
    LobbyDisplaySettings? Settings,
    string? Error);
