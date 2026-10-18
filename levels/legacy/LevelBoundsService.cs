using System;
using Godot;

namespace DAARPG.levels.legacy;

/// <summary>
///     Legacy autoload singleton demonstrating global state
///     and C# event-based bounds notifications.
/// </summary>
/// <remarks>
///     Retained for architectural reference only.
///     No longer used by the active gameplay implementation.
/// </remarks>
[Obsolete(
    "Legacy bounds singleton. Configure PlayerCamera bounds " +
    "through the active LevelLifecycleController instead.",
    false)]
public partial class LevelBoundsService : Node
{
    // Exposing the autoload instance explicitly through a static Instance property.
    public static LevelBoundsService Instance { get; private set; } = null!;

    public Rect2I? CurrentMapBounds { get; private set; }

    // [Signal]
    // public delegate void MapBoundsChangedEventHandler(Rect2I newMapBounds);
    public event Action<Rect2I>? MapBoundsChanged;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        Instance = this;
    }

    /*
     * Store current bounds and notify PlayerCamera.
     */
    public void SetLevelBounds(Rect2I newMapBounds)
    {
        CurrentMapBounds = newMapBounds;
        // EmitSignal(SignalName.MapBoundsChanged, newMapBounds);
        MapBoundsChanged?.Invoke(newMapBounds);
    }
}
