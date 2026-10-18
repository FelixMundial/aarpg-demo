using Godot;

namespace DAARPG.levels.metadata;

[Tool]
[GlobalClass]
public partial class LevelDefinition : Resource
{
    [Export]
    public string Id { get; set; } = string.Empty;

    // [Obsolete(
    //     "Exporting a PackedScene creates a resource dependency;" +
    //     "if two scenes reference one another through exported scene properties," +
    //     " Godot can encounter cyclic dependencies during loading.",
    //     true)]
    // [Export]
    // public PackedScene Scene { get; set; } = null!;

    // A string file reference avoids that dependency edge
    [Export(PropertyHint.File, "*.tscn,")]
    public string SceneResourceReference { get; set; } =
        string.Empty;
}
