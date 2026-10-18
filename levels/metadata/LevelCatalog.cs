using Godot;
using Godot.Collections;

namespace DAARPG.levels.metadata;

[Tool]
[GlobalClass]
public partial class LevelCatalog : Resource
{
    [Export]
    public Array<LevelDefinition> LevelDefinitions { get; set; } = [];
}
