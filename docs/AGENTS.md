# Project Context

This is a Godot 2D action RPG code-along project based on Michael Games' tutorial series. The tutorials use GDScript; this project ports the implementation to C#.

- [Full tutorial series](https://www.youtube.com/playlist?list=PLfcCiyd_V9GH8M9xd_QKlyU8jryGcy3Xa)
- [Episode referenced for this handoff (playlist index 6)](https://www.youtube.com/watch?v=bgSlXYs5dw4&list=PLfcCiyd_V9GH8M9xd_QKlyU8jryGcy3Xa&index=6)

The goal is to learn through the code-along while improving architectural coupling and eventually turning the project into a reusable template for future 2D RPGs. Preserve intended gameplay while favoring clear ownership, explicit dependencies, and maintainable C# designs proportionate to the project's needs.

## Starting a Code Review

- Read [the project architecture overview](Project_Architecture.md). It records the system design, dated implementation snapshot, and material current limitations; verify it against the current code.
- Inspect the relevant scripts and scenes together, especially `player/`and `system`, before judging lifecycle, node paths, state registration, or callback ordering. Current code and scene configuration are the source of truth.
- Review the C# port on its own merits. Identify concrete coupling or responsibility problems and explain how proposed changes support reuse; avoid adding abstractions solely to diverge from the tutorial.
- State what was inspected and whether conclusions were verified by a build or runtime check. Do not treat this context note as a completed architecture review.
- When code changes require an architecture-document update, emphasize reusable system responsibilities and ownership, scene/node/script hierarchy, implementation shape, and normal call flows. Keep helper internals and isolated edge cases concise or omit them unless they block normal use or define an important system boundary; apply the same emphasis to all future architecture-document updates.
