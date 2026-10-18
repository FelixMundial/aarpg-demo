using System;
using DAARPG.core.extensions;
using Godot;

namespace DAARPG.levels.runtime;

public partial class LevelBoundsProvider : TileMapLayer
{
    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        // Bounds calculation and bounds publication should be separated,
        //  in case an invalid new level irreversibly updates the camera.
        // LevelBoundsService.Instance.SetLevelBounds(CalculateLevelBounds());
    }

    /*
     * Calculate bounds from the TileMapLayer, without modifying global state.
     */
    public Rect2I CalculateLevelBounds()
    {
        return !TryCalculateLevelBounds(out var bounds, out var error)
            ? throw new InvalidOperationException(error)
            : bounds;
    }

    public bool TryCalculateLevelBounds(
        out Rect2I bounds,
        out string error)
    {
        bounds = default(Rect2I);

        if (TileSet is not { } tileSet)
        {
            error = $"{Name}: TileSet is not assigned.";
            return false;
        }

        var usedRect = GetUsedRect();
        if (usedRect.Size.X <= 0 || usedRect.Size.Y <= 0)
        {
            error = $"{Name}: No used tile cells are available to calculate level bounds.";
            return false;
        }

        var tileSize = tileSet.TileSize;
        if (tileSize.X <= 0 || tileSize.Y <= 0)
        {
            error = $"{Name}: TileSet has an invalid tile size ({tileSize.X}, {tileSize.Y}).";
            return false;
        }

        var localX = (long)usedRect.Position.X * tileSize.X;
        var localY = (long)usedRect.Position.Y * tileSize.Y;
        var width = (long)usedRect.Size.X * tileSize.X;
        var height = (long)usedRect.Size.Y * tileSize.Y;

        if (localX is < int.MinValue or > int.MaxValue ||
            localY is < int.MinValue or > int.MaxValue ||
            width is <= 0 or > int.MaxValue ||
            height is <= 0 or > int.MaxValue)
        {
            error = $"{Name}: TileMap bounds exceed the supported integer range.";
            return false;
        }

        var localPosition = new Vector2I((int)localX, (int)localY);
        var localSize = new Vector2I((int)width, (int)height);

        var globalPosition = ToGlobal(localPosition);
        if (!globalPosition.X.TryRoundToInt(out var globalX) ||
            !globalPosition.Y.TryRoundToInt(out var globalY))
        {
            error = $"{Name}: Transformed level bounds position is outside the supported integer range.";
            return false;
        }

        if ((long)globalX + localSize.X > int.MaxValue ||
            (long)globalY + localSize.Y > int.MaxValue)
        {
            error = $"{Name}: Transformed level bounds end is outside the supported integer range.";
            return false;
        }

        bounds = new Rect2I(new Vector2I(globalX, globalY), localSize);
        error = string.Empty;
        return true;
    }
}
