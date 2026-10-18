using System;
using DAARPG.core.enums;

namespace DAARPG.core.utils;

public static class Directions
{
    private static readonly CardinalDirection[] CardinalDirections =
        Enum.GetValues<CardinalDirection>();

    public static CardinalDirection PickRandomCardinalDirection()
    {
        return CardinalDirections[Random.Shared.Next(CardinalDirections.Length)];
    }

    public static CardinalDirection ParseCardinalDirection(string facingString)
    {
        return Enum.TryParse<CardinalDirection>(
                   facingString,
                   out var savedFacingDirection) &&
               Enum.IsDefined(savedFacingDirection)
            ? savedFacingDirection
            : CardinalDirection.Down;
    }
}
