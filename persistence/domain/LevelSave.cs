using System.Collections.Generic;

namespace DAARPG.persistence.domain;

public sealed class LevelSave
{
    public required List<string>? PersistentFlags { get; init; } = [];
}
