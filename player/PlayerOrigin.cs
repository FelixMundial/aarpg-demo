using DAARPG.core.enums;
using Godot;

namespace DAARPG.levels;

public readonly record struct PlayerOrigin(
    Vector2 GlobalPosition,
    CardinalDirection FacingDirection
);
