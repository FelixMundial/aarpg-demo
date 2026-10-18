using DAARPG.levels.metadata;
using Godot;

namespace DAARPG.levels.registry;

public sealed record ResolvedLevelDefinition(
    LevelDefinition Definition,
    PackedScene Scene)
{
    public string Id => Definition.Id;
}
