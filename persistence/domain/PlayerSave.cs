namespace DAARPG.persistence.domain;

public sealed class PlayerSave
{
    public float Health { get; init; }

    public float MaxHealth { get; init; }

    public required string? LifeState { get; init; }

    public float PositionX { get; init; }

    public float PositionY { get; init; }

    public required string? FacingDirection { get; init; }
}
