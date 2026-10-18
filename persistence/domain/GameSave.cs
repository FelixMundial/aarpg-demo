using System.Collections.Generic;

namespace DAARPG.persistence.domain;

public sealed class GameSave
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; }

    public required string? CurrentLevelId { get; init; }

    public required PlayerSave? PlayerSave { get; init; }

    public Dictionary<string, LevelSave?>? LevelSaves { get; init; } = [];
}
