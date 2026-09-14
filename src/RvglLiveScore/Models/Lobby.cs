namespace RvglLiveScore.Models;

public sealed record Lobby(string Id, string Name, int PlayerCount);

public sealed record LobbySnapshot(
    IReadOnlyList<Lobby> Lobbies,
    bool IsLoading,
    string? Error,
    DateTimeOffset? LastUpdated);
