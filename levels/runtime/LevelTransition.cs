using System;
using DAARPG.core.enums;
using DAARPG.core.extensions;
using DAARPG.levels.metadata;
using DAARPG.player;
using Godot;

namespace DAARPG.levels.runtime;

[Tool]
public partial class LevelTransition : Area2D
{
    private enum TransitionState
    {
        Ready,
        AwaitingExit
    }

    // Preferred for bidirectional level links.
    // [Export(PropertyHint.File, "*.tscn")]
    // public string TargetLevelScenePath { get; private set; } = string.Empty;
    [Export]
    public LevelDefinition? TargetLevelDefinition { get; private set; }

    [Export]
    public StringName? ExitName { get; private set; }

    [Export]
    public StringName? TargetExitName { get; private set; }

    [Export]
    private Marker2D _playerOriginMarker = null!;

    [Export]
    private CardinalDirection _playerFacingDirection;

    [Export]
    private TileMapLayer? _gridLayer; // Inspector setters may execute before _Ready().

    [ExportCategory("Area Control")]
    [Export(PropertyHint.Range, "1,12,1,or_greater")]
    public int WidthInTiles
    {
        get => _widthInTiles;
        set
        {
            _widthInTiles = Mathf.Max(1, value);
            AdjustCollisionShape();
        }
    }

    [Export]
    public CardinalDirection FacingDirection
    {
        get => _facingDirection;
        set
        {
            _facingDirection = value;
            AdjustCollisionShape();
        }
    }

    // Use "Grid Snap" in the editor instead.
    [ExportToolButton("Snap to Level Grid")]
    private Callable SnapToGridButton => Callable.From(SnapToGrid);

    private int _widthInTiles = 2;

    private CardinalDirection _facingDirection = CardinalDirection.Left;

    private TransitionState _transitionState = TransitionState.Ready;

    private CollisionShape2D? _collisionShape;

    private PlayerOrigin FallbackPlayerOrigin =>
        new(GlobalPosition, ResolvePlayerFacingDirection());

    public event Action<string, StringName>? LevelTransitionTriggered;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        _collisionShape = GetNode<CollisionShape2D>("CollisionShape2D");
        AdjustCollisionShape();

        if (Engine.IsEditorHint())
        {
            return;
        }

        BodyEntered += OnBodyEntered;
        BodyExited += OnBodyExited;

        SetDetectionEnabled(false);
    }

    public bool TryValidate(out string error)
    {
        // if (string.IsNullOrWhiteSpace(TargetLevelScenePath))
        if (TargetLevelDefinition == null)
        {
            error = $"{Name}: Target level is not specified.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(ExitName))
        {
            error = $"{Name}: An exit name is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(TargetExitName))
        {
            error = $"{Name}: A target exit name is required.";
            return false;
        }

        if (!TryGetPlayerOrigin(out _, out var playerOriginError))
        {
            error = $"{Name}: Invalid player origin: {playerOriginError}";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public bool TryGetPlayerOrigin(
        out PlayerOrigin playerOrigin,
        out string error)
    {
        if (_playerOriginMarker is not { } marker)
        {
            error = $"{Name}: Invalid player origin marker.";
            playerOrigin = default(PlayerOrigin);
            return false;
        }

        error = string.Empty;
        playerOrigin = new PlayerOrigin(
            marker.GlobalPosition,
            ResolvePlayerFacingDirection());
        return true;
    }

    /*
     * NOT USED
     * GetOrDefault-style API - Fallback coverage
     *  Missing player origin is covered with a fallback.
     */
    public PlayerOrigin GetPlayerOriginOrDefault()
    {
        if (_playerOriginMarker is { } marker)
        {
            return new PlayerOrigin(
                marker.Position,
                ResolvePlayerFacingDirection());
        }

        GD.PushWarning(
            $"{Name}: Player origin is missing. " +
            $"Fallback to position of the level transition.");
        return FallbackPlayerOrigin;
    }

    public void SuppressForPlayerArrival(Player player)
    {
        _transitionState = TransitionState.AwaitingExit;
    }

    public void RearmIfUnaffectedByPlayerArrival(Player player)
    {
        if (_transitionState == TransitionState.AwaitingExit &&
            !OverlapsBody(player))
        {
            _transitionState = TransitionState.Ready;
        }
    }

    public void SetDetectionEnabled(bool enabled)
    {
        SetDeferred(
            Area2D.PropertyName.Monitoring,
            enabled
        );
    }

    private CardinalDirection ResolvePlayerFacingDirection()
    {
        return (-FacingDirection.ToVector2()).ToCardinalDirection();
    }

    private void AdjustCollisionShape()
    {
        // Inspector setters may execute before _Ready().
        if (_collisionShape is null)
        {
            return;
        }

        if (_collisionShape.Shape is not RectangleShape2D rectangle)
        {
            GD.PushError($"{Name}: A RectangleShape2D collision shape is required.");
            return;
        }

        // Inspector setters may execute before _Ready().
        if (_gridLayer?.TileSet is not { } tileSet)
        {
            GD.PushError($"{Name}: TileSet information is needed.");
            return;
        }

        Vector2 gridCell = new(
            tileSet.TileSize.X,
            tileSet.TileSize.Y
        );

        var horizontal = FacingDirection is
            CardinalDirection.Up or CardinalDirection.Down;

        rectangle.Size = horizontal
            ? new Vector2(gridCell.X * _widthInTiles, gridCell.Y)
            : new Vector2(gridCell.X, gridCell.Y * _widthInTiles);

        var halfGridCell = gridCell / 2f;
        _collisionShape.Position = FacingDirection switch
        {
            CardinalDirection.Up => Vector2.Up * halfGridCell.Y,
            CardinalDirection.Down => Vector2.Down * halfGridCell.Y,
            CardinalDirection.Left => Vector2.Left * halfGridCell.X,
            CardinalDirection.Right => Vector2.Right * halfGridCell.X,
            _ => Vector2.Zero
        };
    }

    /*
     * Use "Grid Snap" in the editor instead.
     */
    private void SnapToGrid()
    {
        // Inspector setters may execute before _Ready().
        if (_gridLayer?.TileSet is not { } tileSet)
        {
            GD.PushError("LevelTransition needs tileSet information.");
            return;
        }

        Vector2 gridCell = new(
            tileSet.TileSize.X,
            tileSet.TileSize.Y
        );

        var snapStep = gridCell / 2f;

        // Plan A. half-cell snapping
        var gridLocalPosition = _gridLayer.ToLocal(GlobalPosition);
        // Position = new Vector2(
        //     Mathf.RoundToInt(Position.X / snapStep) * snapStep,
        //     Mathf.RoundToInt(Position.Y / snapStep) * snapStep);
        var snappedPosition = gridLocalPosition.Snapped(snapStep);
        GlobalPosition = _gridLayer.ToGlobal(snappedPosition);

        // Plan B. full-cell snapping
        // var gridLocal = _gridLayer.ToLocal(GlobalPosition);
        // var cell = _gridLayer.LocalToMap(gridLocal);
        // GlobalPosition = _gridLayer.ToGlobal(_gridLayer.MapToLocal(cell));
    }

    private void OnBodyEntered(Node2D body)
    {
        if (body is not Player player
            || _transitionState != TransitionState.Ready)
        {
            return;
        }

        if (TargetLevelDefinition is { } levelDefinition &&
            !string.IsNullOrWhiteSpace(levelDefinition.Id) &&
            !string.IsNullOrWhiteSpace(TargetExitName))
        {
            LevelTransitionTriggered?.Invoke(
                TargetLevelDefinition.Id,
                TargetExitName);
        }
    }

    private void OnBodyExited(Node2D body)
    {
        if (body is not Player player
            || _transitionState != TransitionState.AwaitingExit)
        {
            return;
        }

        _transitionState = TransitionState.Ready;
    }
}
