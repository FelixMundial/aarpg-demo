namespace DAARPG.system.session.snapshot;

public record WorldSnapshot(
    string CurrentLevelId,
    PlayerSnapshot PlayerSnapshot
);
