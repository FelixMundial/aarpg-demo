using System.Diagnostics.CodeAnalysis;
using DAARPG.core.utils;
using DAARPG.levels;
using DAARPG.levels.controller;
using DAARPG.levels.registry;
using DAARPG.player.enums;
using DAARPG.system.session.snapshot;
using DAARPG.system.spec;
using Godot;

namespace DAARPG.system.service;

public sealed class GameSnapshotAdapter(
    LevelRegistry levelRegistry,
    LevelLifecycleController levelLifecycleController) : IGameSnapshotPort
{
    public bool TryCapture([NotNullWhen(true)] out WorldSnapshot? worldSnapshot,
        out string error)
    {
        GD.Print($"{nameof(GameSnapshotAdapter)}: " +
                 $"Capturing game snapshot...");
        worldSnapshot = null;

        var player = levelLifecycleController.Player;
        var currentLevel = levelLifecycleController.CurrentLevel;
        var currentLevelId = levelLifecycleController.CurrentLevelId;

        if (player.CurrentLifeState == LifeState.Dead)
        {
            error = $"{nameof(GameSnapshotAdapter)}: " +
                    $"Game snapshot while dying is disabled.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(currentLevelId))
        {
            error = $"{nameof(GameSnapshotAdapter)}: " +
                    $"Current level identifier is empty.";
            return false;
        }

        error = string.Empty;
        var playerSnapshot = new PlayerSnapshot(
            player.Health,
            player.MaxHealth,
            player.CurrentLifeState.ToString(),
            player.GlobalPosition.X,
            player.GlobalPosition.Y,
            player.FacingDirection.ToString());
        worldSnapshot = new WorldSnapshot(currentLevelId, playerSnapshot);
        return true;
    }

    public bool TryRestore(GameSnapshot gameSnapshot,
        out string error)
    {
        GD.Print($"{nameof(GameSnapshotAdapter)}: " +
                 $"Restoring from game snapshot...");
        var (currentLevelId, playerSnapshot) = gameSnapshot.WorldSnapshot;

        if (!levelRegistry.TryResolveLevelById(
                currentLevelId,
                out var level,
                out error))
        {
            error = $"{nameof(GameSnapshotAdapter)}: " +
                    $"Could not resolve current level [{currentLevelId}]: {error}.";
            return false;
        }

        var playerSpec = AssemblePlayerRestoreSpec(playerSnapshot);

        // todo Translate game progression information in GameSession inside
        return levelLifecycleController.TryRestoreLevel(
            level,
            playerSpec,
            out error);
    }

    private static PlayerSpec AssemblePlayerRestoreSpec(PlayerSnapshot playerSnapshot)
    {
        // Facing direction falls back to Down.
        var playerOrigin = new PlayerOrigin(
            new Vector2(playerSnapshot.PositionX, playerSnapshot.PositionY),
            Directions.ParseCardinalDirection(playerSnapshot.FacingDirection));
        return new PlayerSpec(
            playerSnapshot.Health,
            playerSnapshot.MaxHealth,
            playerOrigin);
    }
}
