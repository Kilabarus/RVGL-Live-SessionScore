using RvglLiveScore.Models;

namespace RvglLiveScore.Services;

public sealed class LobbyDirectory
{
    private LobbySnapshot _snapshot = new([], true, null, null);
    public LobbySnapshot Snapshot => Volatile.Read(ref _snapshot);

    public void Update(IReadOnlyList<Lobby> lobbies) =>
        Volatile.Write(ref _snapshot, new(lobbies, false, null, DateTimeOffset.UtcNow));

    // Do not present stale lobbies as open when the upstream request failed.
    public void MarkUnavailable() => Volatile.Write(ref _snapshot,
        new([], false, "Unable to load lobbies. Retrying automatically…", Snapshot.LastUpdated));
}
