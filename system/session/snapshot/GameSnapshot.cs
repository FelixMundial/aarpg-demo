using System;

namespace DAARPG.system.session.snapshot;

public sealed class GameSnapshot(
    WorldSnapshot worldSnapshot,
    GameSession gameSession)
{
    public WorldSnapshot WorldSnapshot { get; }
        = worldSnapshot ?? throw new ArgumentNullException(nameof(worldSnapshot));

    // A detached copy, not the active GameSession instance.
    public GameSession GameSession { get; }
        = (gameSession ?? throw new ArgumentNullException(nameof(gameSession)))
        .DeepCopy();
}
