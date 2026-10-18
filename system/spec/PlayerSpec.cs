using DAARPG.levels;

namespace DAARPG.system.spec;

public record PlayerSpec(
    float Health,
    float MaxHealth,
    PlayerOrigin Origin
);
