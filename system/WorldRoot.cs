using System;
using System.Threading.Tasks;
using DAARPG.gui;
using DAARPG.gui.menu;
using DAARPG.gui.transition;
using DAARPG.levels.controller;
using DAARPG.levels.metadata;
using DAARPG.levels.registry;
using DAARPG.player.enums;
using DAARPG.system.controller;
using DAARPG.system.service;
using DAARPG.system.session;
using Godot;

namespace DAARPG.system;

public partial class WorldRoot : Node2D
{
    [Export]
    private PackedScene? _playerScene;

    [Export]
    private string _initialLevelId = string.Empty;
    [Export]
    private LevelCatalog? _levelCatalog;
    private LevelRegistry _levelRegistry = null!;

    private ResolvedLevelDefinition? _initialLevelDefinition;

    // Use "prepared object" pattern for long-lived objects with an initialization phase.
    private PackedScene _validatedPlayerScene = null!;

    private MenuController _menuController = null!;
    private PauseMenu _pauseMenu = null!;

    private GameSession _gameSession = null!;
    private GameSaveService _gameSaveService = null!;
    private WorldStartupRequest _worldStartupRequest = null!;
    private IGameSnapshotPort _gameSnapshotPort = null!;

    private LevelLifecycleController _levelLifecycleController = null!;

    private Node2D _world = null!;

    private Node2D _levels = null!;

    private PlayerHud _playerHud = null!;

    private LevelTransitionOverlay _levelTransitionOverlay = null!;

    private bool _isConfigured;

    private bool _isResumeRequestConnected;
    private bool _isSaveRequestConnected;
    private bool _isLoadRequestConnected;
    private bool _isQuitRequestConnected;

    private bool _isLoading;

    public ResolvedLevelDefinition InitialLevelDefinition =>
        _initialLevelDefinition ?? throw new InvalidOperationException(
            "Initial level is not configured.");

    public bool IsStartupSucceeded { get; private set; }

    public string? StartupError { get; private set; }

    public override void _Ready()
    {
        if (!_isConfigured)
        {
            StartupError = $"{nameof(WorldRoot)} was added to the SceneTree without configuration.";
            return;
        }

        if (!TryPrepare(out var error))
        {
            StartupError = error;
            GD.PushError($"{Name}: {StartupError}");
            return;
        }

        _world = GetNode<Node2D>("World");
        _levels = GetNode<Node2D>("World/Levels");
        _playerHud = GetNode<PlayerHud>("HUD/PlayerHud");

        _levelTransitionOverlay = GetNode<LevelTransitionOverlay>(
            "HUD/LevelTransitionOverlay");

        _pauseMenu = GetNode<PauseMenu>("HUD/PauseMenu");
        _menuController = new MenuController(
            GetTree(),
            _pauseMenu);

        _levelLifecycleController = new LevelLifecycleController(
            _world,
            _levels,
            _validatedPlayerScene);
        _levelLifecycleController.LevelTransitionRequested +=
            HandleLevelTransitionRequested;

        _gameSnapshotPort = new GameSnapshotAdapter(
            _levelRegistry,
            _levelLifecycleController);

        _pauseMenu.GameResumeRequested += _menuController.ResumeGame;
        _isResumeRequestConnected = true;
        _pauseMenu.GameSaveRequested += OnGameSaveRequested;
        _isSaveRequestConnected = true;
        _pauseMenu.GameLoadRequested += OnGameLoadRequested;
        _isLoadRequestConnected = true;
        _pauseMenu.GameQuitRequested += _menuController.QuitGame;
        _isQuitRequestConnected = true;

        Initialize();
    }

    public void Initialize()
    {
        switch (_worldStartupRequest)
        {
            case WorldStartupRequest.NewGame newGame:
                GD.Print($"{Name}: Starting a new game...");
                if (!TryStart(out var startingError))
                {
                    StartupError = startingError;
                    GD.PushError($"{Name}: {StartupError}");
                    return;
                }

                break;

            case WorldStartupRequest.LoadGame loadGame:
                var saveSlotId = loadGame.SaveSlotId;
                GD.Print($"{Name}: Loading save game {saveSlotId}...");
                if (!TryContinue(saveSlotId, out var loadingError))
                {
                    StartupError = loadingError;
                    GD.PushError($"{Name}: {StartupError}");
                    return;
                }

                break;

            default:
                StartupError = $"{Name}: Unsupported world startup request {_worldStartupRequest}.";
                GD.PushError($"{Name}: {StartupError}");
                break;
        }

        IsStartupSucceeded = true;
        GD.Print($"{Name}: {_levelLifecycleController.Player.Name} " +
                 $"is now in [{_levelLifecycleController.CurrentLevelId}].");
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_menuController is not { } menuController)
        {
            return;
        }

        if (@event.IsActionPressed("ui_cancel"))
        {
            menuController.ToggleGamePause();
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _ExitTree()
    {
        if (_pauseMenu is { } pauseMenu && IsInstanceValid(pauseMenu))
        {
            if (_menuController is { } menuController)
            {
                if (_isResumeRequestConnected)
                {
                    pauseMenu.GameResumeRequested -= menuController.ResumeGame;
                    _isResumeRequestConnected = false;
                }

                if (_isQuitRequestConnected)
                {
                    pauseMenu.GameQuitRequested -= menuController.QuitGame;
                    _isQuitRequestConnected = false;
                }
            }

            if (_isSaveRequestConnected)
            {
                pauseMenu.GameSaveRequested -= OnGameSaveRequested;
                _isSaveRequestConnected = false;
            }

            if (_isLoadRequestConnected)
            {
                pauseMenu.GameLoadRequested -= OnGameLoadRequested;
                _isLoadRequestConnected = false;
            }
        }

        if (_levelLifecycleController is { } lifecycleController)
        {
            lifecycleController.LevelTransitionRequested -=
                HandleLevelTransitionRequested;
            lifecycleController.Dispose();
        }
    }

    public void Configure(
        GameSaveService gameSaveService,
        GameSession gameSession,
        WorldStartupRequest worldStartupRequest)
    {
        if (IsInsideTree())
        {
            throw new InvalidOperationException(
                $"{nameof(WorldRoot)} must be configured before entering the scene tree.");
        }

        if (_isConfigured)
        {
            GD.PushError($"{nameof(WorldRoot)} has already been configured.");
        }

        _gameSession = gameSession
                       ?? throw new ArgumentNullException(nameof(gameSession));

        _gameSaveService = gameSaveService
                           ?? throw new ArgumentNullException(nameof(gameSaveService));

        _worldStartupRequest = worldStartupRequest
                               ?? throw new ArgumentNullException(nameof(worldStartupRequest));

        _isConfigured = true;
    }

    private bool TryPrepare(out string error)
    {
        if (_levelCatalog is not { } levelCatalog)
        {
            error = $"{Name}: Level catalog is not configured.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(_initialLevelId))
        {
            error = $"{Name}: Initial level id is not configured.";
            return false;
        }

        _levelRegistry = new LevelRegistry(levelCatalog);
        if (!_levelRegistry.TryResolveLevelById(_initialLevelId,
                out var initialLevelDefinition,
                out error))
        {
            error = $"{Name}: Initial level is not registered.";
            return false;
        }

        _initialLevelDefinition = initialLevelDefinition;

        if (_playerScene is null)
        {
            error = $"{Name}: Player scene is not assigned.";
            return false;
        }

        _validatedPlayerScene = _playerScene;

        error = string.Empty;
        return true;
    }

    /*
     * Resolve World, Level, HUD
             ↓
       Instantiate Player once
             ↓
       Set initial position
             ↓
       Add Player to World
             ↓
       Player._Ready() initializes its components/FSM
             ↓
       Bind HUD to Player
             ↓
       Gameplay
     */
    private bool TryStart(out string error)
    {
        if (!_levelLifecycleController.TryStartLevel(
                InitialLevelDefinition,
                out var startingError))
        {
            error = $"{Name}: Game initialization failed: {startingError}";
            return false;
        }

        _playerHud.Bind(_levelLifecycleController.Player);

        error = string.Empty;
        return true;
    }

    private bool TryContinue(string saveSlotId, out string error)
    {
        if (!_gameSaveService.TryLoadGame(saveSlotId,
                _gameSession,
                _gameSnapshotPort,
                out var loadingError))
        {
            error = $"{Name}: Game loading error: {loadingError}";
            return false;
        }

        _playerHud.Bind(_levelLifecycleController.Player);

        error = string.Empty;
        return true;
    }

    private async Task PerformLoading(string saveSlotId)
    {
        GD.Print($"{Name}: Game loading requested.");
        var isLoadingSuccessful = false;
        try
        {
            await _levelTransitionOverlay.FadeOut();
            _menuController.HideMenu();

            if (!TryContinue(saveSlotId,
                    out var error))
            {
                GD.PushError($"{Name}: Game loading failed: {error}");
                return;
            }

            isLoadingSuccessful = true;
        }
        finally
        {
            try
            {
                if (isLoadingSuccessful)
                {
                    _menuController.ResumeGame();
                }
            }
            finally
            {
                _isLoading = false;
            }

            await _levelTransitionOverlay.FadeIn();

            if (!isLoadingSuccessful)
            {
                _menuController.ShowMenu();
            }
        }
    }

    private async Task PerformLevelChange(string targetLevelId, StringName targetExitName)
    {
        GD.Print($"{Name}: Requesting level change to [{targetLevelId}]...");
        try
        {
            if (!_levelRegistry.TryResolveLevelById(targetLevelId,
                    out var nextLevel,
                    out var error))
            {
                GD.PushError($"{Name}: Level change failed: {error}");
                return;
            }

            await _levelTransitionOverlay.FadeOut();

            _levelLifecycleController.ChangeLevel(nextLevel, targetExitName);
        }
        finally
        {
            await _levelTransitionOverlay.FadeIn();
            _isLoading = false;
        }
    }

    private void OnGameSaveRequested()
    {
        GD.Print($"{Name}: Game saving requested.");
        var player = _levelLifecycleController.Player;
        if (player.CurrentLifeState == LifeState.Dead ||
            Mathf.IsEqualApprox(player.Health, 0f))
        {
            GD.PushWarning($"{Name}: Game saving while dying is disabled.");
            return;
        }

        // todo
        var saveSlotId = "Save-01";
        if (!_gameSaveService.TrySaveGame(saveSlotId,
                _gameSession,
                _gameSnapshotPort,
                out var error))
        {
            GD.PushError($"{Name}: Game saving failed: {error}");
            return;
        }

        _menuController.ResumeGame();
    }

    private async void OnGameLoadRequested()
    {
        if (_isLoading)
        {
            return;
        }

        _isLoading = true;

        // todo
        var saveSlotId = "Save-01";

        try
        {
            await PerformLoading(saveSlotId);
        }
        catch (Exception exception)
        {
            GD.PushError(
                $"{Name}: Unexpected loading error: {exception}");
        }
    }

    private void HandleLevelTransitionRequested(
        string targetLevelId, StringName targetExitName)
    {
        if (_isLoading)
        {
            return;
        }

        _isLoading = true;

        // Deferring the operation ensures that
        //  the scene-changing workflow starts after the physics-related callback.
        Callable.From(() => { _ = PerformLevelChange(targetLevelId, targetExitName); }
        ).CallDeferred();
    }
}
