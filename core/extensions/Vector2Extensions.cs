using System;
using DAARPG.core.enums;
using Godot;

namespace DAARPG.core.extensions;

public static class Vector2Extensions
{
    public static Vector2 ToVector2(this CardinalDirection direction)
    {
        return direction switch
        {
            CardinalDirection.Right => Vector2.Right,
            CardinalDirection.Down => Vector2.Down,
            CardinalDirection.Left => Vector2.Left,
            CardinalDirection.Up => Vector2.Up,
            _ => throw new ArgumentOutOfRangeException(
                nameof(direction), direction, null)
        };
    }

    public static CardinalDirection ToCardinalDirection(this Vector2 direction)
    {
        return direction switch
        {
            _ when direction == Vector2.Right => CardinalDirection.Right,
            _ when direction == Vector2.Left => CardinalDirection.Left,
            _ when direction == Vector2.Up => CardinalDirection.Up,
            _ when direction == Vector2.Down => CardinalDirection.Down,
            _ => throw new ArgumentOutOfRangeException(
                nameof(direction), direction, "Expected a cardinal unit vector.")
        };
    }

    public static CardinalDirection ResolveCardinalDirectionForInput(
        this Vector2 direction,
        CardinalDirection currentFacing,
        float facingBias = 0.1f)
    {
        if (direction == Vector2.Zero)
        {
            return currentFacing;
        }

        // 当同时有多方向输入时，优先面向当前朝向。其中 facingBias 可以理解为当前朝向的附加权重
        var biasedDirection = direction.Normalized() + currentFacing.ToVector2() * facingBias;

        if (Mathf.Abs(biasedDirection.X) >=
            Mathf.Abs(biasedDirection.Y))
        {
            return biasedDirection.X > 0
                ? CardinalDirection.Right
                : CardinalDirection.Left;
        }

        return biasedDirection.Y > 0
            ? CardinalDirection.Down
            : CardinalDirection.Up;
    }

    public static CardinalDirection Opposite(
        this CardinalDirection direction)
    {
        return direction switch
        {
            CardinalDirection.Up => CardinalDirection.Down,
            CardinalDirection.Down => CardinalDirection.Up,
            CardinalDirection.Left => CardinalDirection.Right,
            CardinalDirection.Right => CardinalDirection.Left,
            _ => throw new ArgumentOutOfRangeException(
                nameof(direction), direction, null)
        };
    }

    public static bool IsInside(this Vector2 position, Rect2I bounds)
    {
        return position.X >= bounds.Position.X &&
               position.Y >= bounds.Position.Y &&
               position.X < bounds.End.X &&
               position.Y < bounds.End.Y;
    }

    public static CardinalDirection ResolveCardinalDirectionForAnalogInput(
        this Vector2 direction,
        CardinalDirection currentFacing,
        float facingBias = 0.1f)
    {
        if (direction == Vector2.Zero)
        {
            return currentFacing;
        }

        CardinalDirection[] dir4 =
        [
            CardinalDirection.Right,
            CardinalDirection.Down,
            CardinalDirection.Left,
            CardinalDirection.Up
        ];

        /*
         * movement direction
                  +
           small current-facing bias
                  ↓
           convert vector → angle
                  ↓
           convert full rotation → four sectors
                  ↓
           round to nearest sector
                  ↓
           RIGHT / DOWN / LEFT / UP
         */
        var biasedDirection = direction.Normalized() + currentFacing.ToVector2() * facingBias;

        var directionId = (int)Mathf.Round(
            biasedDirection.Angle()
            / Mathf.Tau
            * dir4.Length);

        directionId =
            (directionId % dir4.Length + dir4.Length)
            % dir4.Length;

        return dir4[directionId];
    }
}
