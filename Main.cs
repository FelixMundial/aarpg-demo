using DAARPG.system;
using DAARPG.system.bootstrap;
using DAARPG.system.session;
using Godot;

namespace DAARPG;

public partial class Main : Node
{
    [Export]
    private PackedScene? _worldRootScene;

    private WorldRoot? _worldRoot;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        if (_worldRootScene is null)
        {
            GD.PushError($"{Name}: " +
                         $"{nameof(WorldRoot)} is not assigned.");
            return;
        }

        var screenHost = GetNode<Node>("ScreenHost");

        // todo construct a fresh GameSession or load an existing one
        var worldStartupRequest = new WorldStartupRequest.NewGame();

        var gameSession = new GameSession();
        var gameSaveService = GameComposition.CreateGameSaveService();

        var candidateWorldRoot =
            _worldRootScene.Instantiate<WorldRoot>();

        candidateWorldRoot.Configure(
            gameSaveService,
            gameSession,
            worldStartupRequest);

        screenHost.AddChild(candidateWorldRoot);

        if (!candidateWorldRoot.IsStartupSucceeded ||
            !string.IsNullOrWhiteSpace(candidateWorldRoot.StartupError))
        {
            var error = candidateWorldRoot.StartupError
                        ?? $"{nameof(WorldRoot)} startup failed for unlisted reasons.";

            screenHost.RemoveChild(candidateWorldRoot);
            candidateWorldRoot.QueueFree();

            GD.PushError(error);
            return;
        }

        _worldRoot = candidateWorldRoot;
    }
}
