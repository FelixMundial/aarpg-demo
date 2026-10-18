using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DAARPG.core.enums;
using DAARPG.core.extensions;
using DAARPG.levels.runtime;
using DAARPG.player;
using Godot;

namespace DAARPG.levels;

public partial class Level : Node2D
{
    [Export]
    private Marker2D? _defaultPlayerSpawnMarker;

    [Export]
    private CardinalDirection _defaultPlayerFacingDirection = CardinalDirection.Down;

    [Export]
    private Node? _transitionsRoot;

    [Export]
    private LevelBoundsProvider? _levelBoundsProvider;

    [Export]
    private TileMapLayer? _gridLayer;

    // Use "prepared object" pattern for long-lived objects with an initialization phase.
    private LevelBoundsProvider _validatedBoundsProvider = null!;

    private Dictionary<StringName, LevelTransition> _levelTransitionsByName = [];

    private bool _transitionsActive;

    public Vector2 GridCell
    {
        get
        {
            // todo
            var tileSet = _gridLayer!.TileSet;

            return new Vector2(
                tileSet.TileSize.X,
                tileSet.TileSize.Y
            );
        }
    }

    // todo Keep?
    public Rect2I LevelBounds { get; private set; }

    public event Action<string, StringName>? LevelTransitionRelayed;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
    }

    public bool TryPrepare(out string error)
    {
        // 1. Validate required exported references.
        if (_defaultPlayerSpawnMarker is null)
        {
            error = $"{Name}: Default player spawn marker is missing.";
            return false;
        }

        if (_levelBoundsProvider is null)
        {
            error = $"{Name}: Level bounds provider is missing.";
            return false;
        }

        _validatedBoundsProvider = _levelBoundsProvider;

        // 2. Validate the TileSet and default spawn marker.
        if (_gridLayer is not { } gridLayer)
        {
            error = $"{Name}: GridLayer is not assigned.";
            return false;
        }

        if (gridLayer.TileSet is not { } tileSet)
        {
            error = $"{Name}: GridLayer has no TileSet.";
            return false;
        }

        // 3. Validate level bounds.
        if (!TryGetLevelBounds(out var levelBounds, out var levelBoundsError))
        {
            error = $"{Name}: {levelBoundsError}";
            return false;
        }

        LevelBounds = levelBounds;

        // 4. Build the dictionary.
        return TryBuildLevelTransitionRegistry(out error);
    }

    /*
     * GetRequired-style API - Mandatory resolution;
     *  throw exceptions on lookup failure
     * Missing default player spawn point is intolerable.
     */
    public PlayerOrigin GetRequiredInitialPlayerOrigin()
    {
        if (_defaultPlayerSpawnMarker is { } marker)
        {
            return new PlayerOrigin(marker.GlobalPosition,
                _defaultPlayerFacingDirection);
        }

        throw new InvalidOperationException(
            $"{Name}: Default initial player origin is not specified.");
    }

    /*
     * Try-style API - Optional lookup with a flag;
     *  The caller knows whether the lookup succeeded.
     *  (No logging within, the caller handles logging.)
     * Missing level transition entry is expected and covered.
     */
    public bool TryGetPlayerOrigin(
        StringName? targetExitName,
        out PlayerOrigin playerOrigin,
        out string error)
    {
        if (string.IsNullOrWhiteSpace(targetExitName)
            || !_levelTransitionsByName.TryGetValue(
                targetExitName, out var levelTransition))
        {
            error = $"{Name}: No level transition found for {targetExitName}.";
            playerOrigin = default(PlayerOrigin);
            return false;
        }

        return levelTransition.TryGetPlayerOrigin(
            out playerOrigin, out error);
    }

    public Rect2I GetRequiredLevelBounds()
    {
        return _validatedBoundsProvider.CalculateLevelBounds();
    }

    public bool TryGetLevelBounds(
        out Rect2I levelBounds,
        out string error)
    {
        if (_levelBoundsProvider is not { } boundsProvider)
        {
            error = $"{Name}: Level bounds provider is missing.";
            levelBounds = default(Rect2I);
            return false;
        }

        return boundsProvider.TryCalculateLevelBounds(
            out levelBounds, out error);
    }

    public void SuppressTransitionForPlayerArrival(Player player, StringName targetExitName)
    {
        if (_levelTransitionsByName.TryGetValue(
                targetExitName, out var levelTransition))
        {
            levelTransition.SuppressForPlayerArrival(player);
        }

        _ = ReconcileUnaffectedTransitions(player);
    }

    public void SuppressTransitionForPlayerArrival(Player player)
    {
        foreach (var levelTransition in _levelTransitionsByName.Values)
        {
            levelTransition.SuppressForPlayerArrival(player);
        }

        _ = ReconcileUnaffectedTransitions(player);
    }

    public void ActivateLevelTransitions()
    {
        if (_transitionsActive)
        {
            return;
        }

        foreach (var levelTransition in _levelTransitionsByName.Values)
        {
            levelTransition.LevelTransitionTriggered +=
                HandleLevelTransitionTriggered;

            levelTransition.SetDetectionEnabled(true);
        }

        _transitionsActive = true;
    }

    public void DeactivateLevelTransitions()
    {
        if (!_transitionsActive)
        {
            return;
        }

        _transitionsActive = false;

        foreach (var levelTransition in _levelTransitionsByName.Values)
        {
            levelTransition.SetDetectionEnabled(false);

            levelTransition.LevelTransitionTriggered -=
                HandleLevelTransitionTriggered;
        }
    }

    private async Task ReconcileUnaffectedTransitions(Player player)
    {
        try
        {
            var sceneTree = GetTree();
            while (sceneTree.Paused)
            {
                await ToSignal(sceneTree, SceneTree.SignalName.ProcessFrame);
            }

            // Wait for deferred monitoring changes and overlap data to settle.
            //  Two PhysicsFrame awaits are intentional,
            //  a conservative way to get one full physics update after deferred monitoring is enabled.
            await ToSignal(sceneTree, SceneTree.SignalName.PhysicsFrame);
            await ToSignal(sceneTree, SceneTree.SignalName.PhysicsFrame);

            foreach (var levelTransition in _levelTransitionsByName.Values)
            {
                levelTransition.RearmIfUnaffectedByPlayerArrival(player);
            }
        }
        catch (Exception exception)
        {
            GD.PushError(
                $"{Name}: Failed to reconcile transition arrival: " +
                exception.Message);
        }
    }

    private bool TryBuildLevelTransitionRegistry(out string error)
    {
        var levelTransitions = new Dictionary<StringName, LevelTransition>();

        var transitionsRoot = _transitionsRoot ?? this;
        // Discover LevelTransition nodes.
        foreach (var levelTransition in transitionsRoot.GetDescendantsOfType<LevelTransition>())
        {
            var levelTransitionName = levelTransition.ExitName;
            // Validate their entrance IDs and player origins.
            if (!levelTransition.TryValidate(out var internalError))
            {
                error = $"{Name}: Invalid level transition " +
                        $"[{levelTransitionName}]: {internalError}";
                return false;
            }

            if (!levelTransitions.TryAdd(levelTransitionName!, levelTransition))
            {
                // Reject duplicate level transitions.
                error = $"{Name}: Duplicate level transition: {levelTransitionName}.";
                return false;
            }
        }

        // Ideally, the entrance registry is constructed in a temporary dictionary
        //  and published only if all checks succeed.
        _levelTransitionsByName = levelTransitions;

        error = string.Empty;
        return true;
    }

    private void HandleLevelTransitionTriggered(
        string targetLevelId, StringName targetExitName)
    {
        LevelTransitionRelayed?.Invoke(
            targetLevelId,
            targetExitName
        );
    }
}
