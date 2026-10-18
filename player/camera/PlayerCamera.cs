using System;
using Godot;

namespace DAARPG.player.camera;

public partial class PlayerCamera : Camera2D
{
    private Rect2I? _levelBounds;

    public Rect2I LevelBounds =>
        _levelBounds ?? throw new ArgumentNullException(nameof(_levelBounds));

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        // LevelBoundsService.Instance.MapBoundsChanged += HandleMapBoundsChanged;
        // if (LevelBoundsService.Instance.CurrentMapBounds is { } levelBounds)
        // {
        //     ApplyLimits(levelBounds);
        // }

        if (_levelBounds is null)
        {
            GD.PushError($"{Name}: PlayerCamera: Level bounds is not configured.");
            return;
        }

        ApplyLimits(LevelBounds);
    }

    /*
     * The _ExitTree() unsubscribe is important here because the publisher is an autoload
     *	and therefore outlives ordinary level/player scenes.
     * Otherwise, its C# event can retain a delegate referring to a camera that has left the scene tree.
     */
    public override void _ExitTree()
    {
        // LevelBoundsService.Instance.MapBoundsChanged -= HandleMapBoundsChanged;
    }

    public void SetLevelBounds(Rect2I levelBounds)
    {
        _levelBounds = levelBounds;

        // Before the camera enters the scene tree, it only stores the bounds.
        // _Ready() applies them once the camera is initialized.
        if (IsNodeReady())
        {
            ApplyLimits(levelBounds);
        }
    }

    private void ApplyLimits(Rect2I levelBounds)
    {
        LimitLeft = levelBounds.Position.X;
        LimitTop = levelBounds.Position.Y;
        LimitRight = levelBounds.Position.X + levelBounds.Size.X;
        LimitBottom = levelBounds.Position.Y + levelBounds.Size.Y;
    }

    // private void HandleMapBoundsChanged(Rect2I bounds)
    // {
    //     ApplyLimits(bounds);
    // }
}
