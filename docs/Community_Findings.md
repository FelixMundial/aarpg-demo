# Community Findings

<https://forum.godotengine.org/t/my-journey-wrestling-with-dependency-injection-in-godot-c-projects/132379>


## Chickensoft AutoInject <https://github.com/chickensoft-games/AutoInject>

A maintained, Godot-specific C# solution using source generators and scene-tree-aware provider/dependent relationships. It supports injecting dependencies from ancestor nodes without relying on runtime reflection. Particularly relevant if our future RPG template gains many nested scenes needing shared services.

## Godot C# Dependency Injection <https://github.com/Nico1395/godot-addons-dependency-injection>

A community implementation built around standard .NET DI interfaces and IServiceCollection. It demonstrates that Microsoft's DI architecture can be integrated into Godot, including dependency registration through a startup node. Its author notes limitations around scene lifecycle and service scopes.

