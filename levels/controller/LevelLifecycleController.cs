using System;
using System.Diagnostics.CodeAnalysis;
using DAARPG.core.extensions;
using DAARPG.levels.registry;
using DAARPG.levels.runtime;
using DAARPG.player;
using DAARPG.system.spec;
using Godot;

namespace DAARPG.levels.controller;

/*
 * Manage the current references and the mechanics of replacing scenes.
 */
public sealed class LevelLifecycleController(
    Node2D world,
    Node2D levels,
    PackedScene __playerScene) : IDisposable
{
    private readonly Node2D _world =
        world ?? throw new ArgumentNullException(nameof(world));

    private readonly Node2D _levels =
        levels ?? throw new ArgumentNullException(nameof(levels));

    private readonly PackedScene _playerScene =
        __playerScene ?? throw new ArgumentNullException(nameof(__playerScene));

    private Level? _currentLevel;

    private Player? _player;

    private bool _isDisposed;

    public Player Player =>
        _player ?? throw new InvalidOperationException(
            "No active Player.");

    public Level CurrentLevel =>
        _currentLevel ?? throw new InvalidOperationException(
            "No active Level.");

    public ResolvedLevelDefinition? CurrentLevelDefinition { get; private set; }

    public string? CurrentLevelId => CurrentLevelDefinition?.Id;

    public event Action<string, StringName>?
        LevelTransitionRequested;

    public bool TryStartLevel(
        ResolvedLevelDefinition initialLevelDefinition,
        out string error)
    {
        GD.Print($"{nameof(LevelLifecycleController)}: " +
                 $"Starting level...");
        if (!TryPrepareLevel(initialLevelDefinition,
                out var candidateLevel,
                out var preparationError))
        {
            error = $"{nameof(LevelLifecycleController)}: " +
                    $"Could not start level: {preparationError}";
            return false;
        }

        _currentLevel = candidateLevel;
        CurrentLevelDefinition = initialLevelDefinition;

        // Create the Player only once per session.
        var playerOrigin = CurrentLevel.GetRequiredInitialPlayerOrigin();
        if (!TryInitializePlayer(playerOrigin,
                out var player, out var initializationError))
        {
            error = $"{nameof(LevelLifecycleController)}: " +
                    $"Could not initialize the player: {initializationError}";
            _currentLevel = null;
            CurrentLevelDefinition = null;
            _levels.RemoveChild(candidateLevel);
            candidateLevel.Free();
            return false;
        }

        CurrentLevel.LevelTransitionRelayed +=
            HandleLevelTransitionRelayed;
        CurrentLevel.ActivateLevelTransitions();

        error = string.Empty;
        _player = player;
        return true;
    }

    public bool TryRestoreLevel(
        ResolvedLevelDefinition loadedLevelDefinition,
        PlayerSpec playerSpec,
        out string error)
    {
        GD.Print($"{nameof(LevelLifecycleController)}: " +
                 $"Restoring level...");
        if (!TryPrepareLevel(loadedLevelDefinition,
                out var candidateLevel,
                out var preparationError))
        {
            error = $"{nameof(LevelLifecycleController)}: " +
                    $"Could not restore level: {preparationError}";
            return false;
        }

        var playerToRefresh = _player;
        bool isPlayerLoaded;
        string playerLoadingError;

        if (playerToRefresh != null)
        {
            isPlayerLoaded = TryRestorePlayer(playerToRefresh,
                candidateLevel, playerSpec,
                out playerLoadingError);
        }
        else
        {
            isPlayerLoaded = TryLoadPlayer(candidateLevel, playerSpec,
                out playerToRefresh, out playerLoadingError);
        }

        if (!isPlayerLoaded || playerToRefresh is null)
        {
            error = $"{nameof(LevelLifecycleController)}: " +
                    $"Could not load player: {playerLoadingError}";
            _levels.RemoveChild(candidateLevel);
            candidateLevel.Free();
            return false;
        }

        _player = playerToRefresh;

        var previousLevel = _currentLevel;
        if (previousLevel is { })
        {
            previousLevel.DeactivateLevelTransitions();
            previousLevel.LevelTransitionRelayed -= HandleLevelTransitionRelayed;
            _levels.RemoveChild(previousLevel);
        }

        _currentLevel = candidateLevel;
        CurrentLevelDefinition = loadedLevelDefinition;

        CurrentLevel.LevelTransitionRelayed +=
            HandleLevelTransitionRelayed;
        CurrentLevel.SuppressTransitionForPlayerArrival(Player);
        CurrentLevel.ActivateLevelTransitions();

        previousLevel?.QueueFree();

        error = string.Empty;
        return true;
    }

    // todo Try?
    public void ChangeLevel(ResolvedLevelDefinition targetLevelDefinition, StringName targetExitName)
    {
        GD.Print($"{nameof(LevelLifecycleController)}: " +
                 $"Changing level to [{targetLevelDefinition.Id}]...");
        if (!TryPrepareLevel(targetLevelDefinition,
                out var candidateLevel,
                out var preparationError))
        {
            GD.PrintErr($"{nameof(LevelLifecycleController)}: " +
                        $"{preparationError}");
            return;
        }

        var isNextLevelValid = true;
        if (!candidateLevel.TryGetPlayerOrigin(targetExitName,
                out var playerOrigin,
                out var playerOriginError))
        {
            GD.PushError($"{nameof(LevelLifecycleController)}: " +
                         $"Cannot resolve level transition " +
                         $"{targetExitName} : {playerOriginError}");
            isNextLevelValid = false;
        }

        if (!isNextLevelValid)
        {
            _levels.RemoveChild(candidateLevel);
            candidateLevel.QueueFree();
            return;
        }

        // Commit Phase
        var previousLevel = CurrentLevel;
        // Remove the old level from the active tree.
        previousLevel.DeactivateLevelTransitions();
        previousLevel.LevelTransitionRelayed -= HandleLevelTransitionRelayed;
        _levels.RemoveChild(previousLevel);

        // Activate the replacement level.
        _currentLevel = candidateLevel;
        CurrentLevelDefinition = targetLevelDefinition;
        GD.Print($"{nameof(LevelLifecycleController)}: " +
                 $"Entering level [{CurrentLevelId}].");

        Player.GlobalPosition = playerOrigin.GlobalPosition;
        Player.SetFacingDirection(playerOrigin.FacingDirection);

        Player.ResetState();

        /*
         * Publish the new level bounds.
           It should happen after player relocation,
            because the camera will now update itself in its MapBoundsChanged event callback,
            and if new level bounds publication happens too early,
            the camera still remaining in the old world would apply incorrect level bounds.
         */
        // LevelBoundsService.Instance.SetLevelBounds(
        //     CurrentLevel.LevelBounds);
        Player.SetLevelBounds(CurrentLevel.LevelBounds);

        CurrentLevel.LevelTransitionRelayed += HandleLevelTransitionRelayed;
        // Don't enable re-entry if player hasn't left the transition area.
        CurrentLevel.SuppressTransitionForPlayerArrival(Player, targetExitName);
        CurrentLevel.ActivateLevelTransitions();
        GD.Print($"{nameof(LevelLifecycleController)}: " +
                 $"{Player.Name} enters [{CurrentLevelId}].");

        // Avoid marking the old Level for destruction
        //  before the rest of the commit has succeeded.
        previousLevel.QueueFree();
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        if (_currentLevel is not { } level ||
            !GodotObject.IsInstanceValid(level))
        {
            return;
        }

        // Disconnect the controller's subscription.
        level.LevelTransitionRelayed -=
            HandleLevelTransitionRelayed;

        // Release the Level's own transition subscriptions.
        // Appropriate while those nodes are still valid.
        level.DeactivateLevelTransitions();

        // Do not RemoveChild(), Free(), or QueueFree() here.
    }

    private bool TryInitializePlayer(PlayerOrigin playerOrigin,
        [NotNullWhen(true)] out Player? player,
        out string error)
    {
        GD.Print($"{nameof(LevelLifecycleController)}: " +
                 $"Initializing player...");
        if (!_playerScene.TryInstantiateScene(out player,
                out var instantiationError))
        {
            error = $"{nameof(LevelLifecycleController)}: " +
                    $"Could not instantiate Player: {instantiationError}";
            return false;
        }

        // Configure local transform before the player enters the scene tree,
        //  so that no further position adjustment is required for this level.
        // World-local conversion preserves the desired global position.
        player.Position = _world.ToLocal(playerOrigin.GlobalPosition);
        player.ConfigureInitialFacingDirection(playerOrigin.FacingDirection);

        /*
         * Publishes the level bounds for the camera to subscribe to.
           It should happen before player enters the tree,
            because the camera is tied to the player, and will update itself in its _Ready(),
            and level bounds publication needs to be ready beforehand.
         */
        // LevelBoundsService.Instance.SetLevelBounds(
        //     CurrentLevel.LevelBounds);
        // Use direct function call rather than event publication.
        player.SetLevelBounds(CurrentLevel.LevelBounds);

        // Add the player to the scene tree when its position/orientation is configured.
        _world.AddChild(player);

        error = string.Empty;
        return true;
    }

    private bool TryLoadPlayer(Level currentLevel,
        PlayerSpec playerSpec,
        [NotNullWhen(true)] out Player? player,
        out string error)
    {
        GD.Print($"{nameof(LevelLifecycleController)}: " +
                 $"Loading player...");
        if (!_playerScene.TryInstantiateScene(
                out player, out var instantiationError))
        {
            error = $"{nameof(LevelLifecycleController)}: " +
                    $"Could not instantiate Player: {instantiationError}";
            return false;
        }

        var (globalPosition, cardinalDirection) = playerSpec.Origin;

        if (!globalPosition.IsFinite() ||
            !globalPosition.IsInside(currentLevel.LevelBounds))
        {
            // Position falls back to the level's default position.
            GD.PushError($"{nameof(LevelLifecycleController)}: " +
                         $"Invalid player position: {globalPosition}");
            globalPosition = currentLevel.GetRequiredInitialPlayerOrigin().GlobalPosition;
        }

        player.Position = _world.ToLocal(globalPosition);
        player.ConfigureInitialFacingDirection(cardinalDirection);

        // LevelBoundsService.Instance.SetLevelBounds(
        //     currentLevel.LevelBounds);
        player.SetLevelBounds(currentLevel.LevelBounds);

        _world.AddChild(player);

        player.TryUpdateMaxHealth(playerSpec.MaxHealth);
        player.SetHealth(playerSpec.Health);

        // Save data contains no in-progress combat state,
        //  so restore the player to a clean, controllable state.
        player.ResetState();

        error = string.Empty;
        return true;
    }

    private bool TryRestorePlayer(Player player,
        Level currentLevel,
        PlayerSpec playerSpec,
        out string error)
    {
        GD.Print($"{nameof(LevelLifecycleController)}: " +
                 $"Restoring player...");
        var (globalPosition, cardinalDirection) = playerSpec.Origin;

        if (!globalPosition.IsFinite() ||
            !globalPosition.IsInside(currentLevel.LevelBounds))
        {
            GD.PushError($"{nameof(LevelLifecycleController)}: " +
                         $"Invalid player position: {globalPosition}");
            globalPosition = currentLevel.GetRequiredInitialPlayerOrigin()
                .GlobalPosition;
        }

        player.GlobalPosition = globalPosition;
        player.SetFacingDirection(cardinalDirection);

        player.TryUpdateMaxHealth(playerSpec.MaxHealth);
        player.SetHealth(playerSpec.Health);

        player.ResetState();

        // LevelBoundsService.Instance.SetLevelBounds(
        //     currentLevel.LevelBounds);
        player.SetLevelBounds(currentLevel.LevelBounds);

        error = string.Empty;
        return true;
    }

    // todo Same level?
    private bool TryPrepareLevel(ResolvedLevelDefinition candidateLevelDefinition,
        [NotNullWhen(true)] out Level? candidateLevel,
        out string error)
    {
        GD.Print($"{nameof(LevelLifecycleController)}: " +
                 $"Preparing level [{candidateLevelDefinition.Id}]...");
        // Preparation Phase:
        // Do not mutate the state of the player or the currently active Level.
        // Instantiate before removing the existing level.
        if (!candidateLevelDefinition.Scene
                .TryInstantiateScene(out candidateLevel,
                    out var instantiationError))
        {
            error = $"{nameof(LevelLifecycleController)}: " +
                    $"Could not instantiate Level: {instantiationError}";
            return false;
        }

        // Prevalidation of scene structure can be performed here.
        if (!candidateLevel.TryPrepare(out var preparationError))
        {
            error = $"{nameof(LevelLifecycleController)}: " +
                    $"Level preparation failed: {preparationError}";
            candidateLevel.Free();
            return false;
        }

        // Suppress immediate re-entry until the Player leaves the destination trigger.
        candidateLevel.DeactivateLevelTransitions();

        // Temporarily stage the candidate Level so its transition nodes have
        //  initialized global transforms and runtime signal setup before the
        //  destination PlayerOrigin is resolved.
        _levels.AddChild(candidateLevel);

        error = string.Empty;
        return true;
    }

    private void HandleLevelTransitionRelayed(
        string targetLevelId, StringName targetExitName)
    {
        LevelTransitionRequested?.Invoke(
            targetLevelId,
            targetExitName);
    }
}
