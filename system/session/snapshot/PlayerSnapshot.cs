namespace DAARPG.system.session.snapshot;

public sealed record PlayerSnapshot(
    float Health,
    float MaxHealth,
    string LifeState,
    float PositionX,
    float PositionY,
    string FacingDirection
);
