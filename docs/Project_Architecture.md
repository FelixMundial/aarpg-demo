# Project Architecture (Godot C#)

Snapshot: 2026-10-19. This document records the current project structure,
system ownership, main scene relationships, and normal gameplay call flows. It
is intended as reusable context for future work on this tutorial-derived 2D
action RPG template. The Level script locations and their scene/resource
references were checked against the current files after relocation. Build and
runtime checks were not rerun after the relocation; interactive gameplay and
save restoration have not been manually validated in this snapshot.

## Project Scope and Runtime Entry

This is a C# port of Michael Games' Godot 4 2D action RPG tutorial series. The
code is being shaped into a reusable template while keeping its systems
proportionate to the current project.

Godot starts `main.tscn`. `Main` is the application entry point: it creates the
game session, save service, and startup request, then instantiates and
configures the `WorldRoot` scene before adding it under `ScreenHost`. `WorldRoot`
is the in-game composition root: it reads the authored Player scene, initial
Level ID, and LevelCatalog; creates the runtime controllers and snapshot
adapter; owns the persistent UI; and delegates Level and Player creation or
replacement to `LevelLifecycleController`.

```text
Project settings (project.godot)
└── Main (main.tscn; Node)
    └── ScreenHost (Node)
        └── WorldRoot scene instance (system/world_root.tscn)
            ├── HUD (Node2D)
            │   ├── PlayerHud (CanvasLayer)
            │   ├── LevelTransitionOverlay (CanvasLayer)
            │   └── PauseMenu (CanvasLayer)
            └── World (Node2D; y-sort enabled)
                ├── Levels (Node2D; y-sort enabled)
                │   └── active or staged Level instances
                └── persistent Player instance
                    ├── PlayerCamera
                    └── InteractionRoot (HitBox and HurtBox)
```

`main.tscn` assigns `system/world_root.tscn` and provides `ScreenHost` as its
attachment point. The WorldRoot scene root is authored with the node name
`WorldRoot` and the `WorldRoot` script. It assigns the Player scene, initial ID
`playground`, and a LevelCatalog containing the `playground` and `grassland`
definitions. Each authored `LevelDefinition` pairs a stable ID with a scene
resource reference. `LevelRegistry` loads those references as PackedScenes and
builds a lookup of `ResolvedLevelDefinition` values by ID. `Main` currently
starts with `WorldStartupRequest.NewGame`; the request type also represents a
startup load, and the pause menu provides the current in-game load path.
`ScreenHost` currently contains one WorldRoot instance and provides the
attachment point for replacing the entire gameplay screen in a future title or
menu flow.

Project settings use a 576×324 logical viewport, a 1920×1080 window override,
viewport stretching with aspect expansion, and the compatibility renderer.
Movement uses WASD and arrow keys; attack uses the left mouse button and P. The
configured 2D physics layers include Player, PlayerHurtBox, Environment, and
Enemy. The project targets Godot .NET 4.7.2 and .NET 8 by default, with .NET 9
for Android.

## Source and Content Map

| Area | Responsibility |
| --- | --- |
| Project root | `Main.cs` and `main.tscn` bootstrap a configured WorldRoot under `ScreenHost`. |
| `system/` | `WorldRoot` gameplay composition, startup request types, runtime sessions/snapshots, `GameSnapshotAdapter`, `GameSaveService`, menu controller, and save-service composition. |
| `levels/` | `Level.cs` defines shared Level content behavior; `controller/` owns Level and persistent Player lifecycle. |
| `levels/metadata/` | Authored `LevelCatalog` and `LevelDefinition` resources. |
| `levels/registry/` | `LevelRegistry` and `ResolvedLevelDefinition` provide stable-ID lookup over authored Level references. |
| `levels/runtime/` | `LevelTransition` portal areas and `LevelBoundsProvider` tilemap bounds. |
| `levels/definitions/`, `levels/plain/`, `levels/playground/` | Authored Level definitions and the Grassland and Playground scene scripts/scenes. |
| `player/` | Player actor and scene, camera, animation/direction enums, and scene-owned state machine and states. |
| `enemies/` | Slime actor and its scene-owned state machine and states. |
| `combat/` | Reusable HitBox and HurtBox Area2D scenes/scripts and the `HitData` payload. |
| `environment/` | Grass TileMapLayer and damageable Plant prop. |
| `gui/` | Player HUD/health slots, pause menu, and Level transition fade overlay. |
| `persistence/` | JSON save DTOs, repository abstraction/implementation, and serializer abstraction. |
| `core/` | Direction types and small shared extensions/utilities. |
| `levels/legacy/` | Retained `LevelBoundsService` example; active camera bounds are passed directly rather than published by this service. |

The authored Level scenes are `levels/playground/playground.tscn` and
`levels/plain/grassland.tscn`. Playground contains the Grass TileMapLayer,
Plants, Slimes, a default Player spawn, and two transitions. Grassland contains
Grass, its default spawn, and one transition.

## Runtime Ownership and Main Call Flows

### Startup and persistent world objects

`Main._Ready()` creates a fresh `GameSession`, the save service, and the
`WorldStartupRequest` (currently `NewGame`). It instantiates `WorldRoot`, calls
`Configure()` while the instance is outside the SceneTree, and then adds it to
`ScreenHost`. Adding it runs `WorldRoot._Ready()` synchronously. The WorldRoot
validates its catalog, initial Level ID, and Player scene; resolves the initial
definition; creates the menu controller, lifecycle controller, and snapshot
adapter; connects PauseMenu and Level-transition events; then calls
`Initialize()`.

```text
Godot → Main._Ready()
  → create GameSession, GameSaveService, WorldStartupRequest.NewGame
  → instantiate WorldRoot; Configure(...) before tree entry
  → ScreenHost.AddChild(WorldRoot)
    → WorldRoot._Ready()
      → TryPrepare(): create LevelRegistry and resolve initial ResolvedLevelDefinition
      → resolve HUD, overlay, menu, World and Levels node references
      → create runtime controllers and snapshot adapter; connect event handlers
      → Initialize(): dispatch the configured startup request
        → TryStart() → LevelLifecycleController.TryStartLevel()
          → prepare and stage initial Level
          → resolve Level's default PlayerOrigin
          → instantiate/configure Player and camera bounds
          → add Player under World; Player initializes its components and state machine
          → activate Level transitions
        → bind PlayerHud to the persistent Player
      → set IsStartupSucceeded or StartupError
  → keep the WorldRoot on success; remove and free it on startup failure
```

The active Level is a child of `World/Levels`. The Player is added once under
`World` and remains there during ordinary Level transitions and in-game loads.
The HUD and overlay also remain in the WorldRoot scene while Level instances
are replaced. If WorldRoot startup fails, `Main` removes the failed instance;
`WorldRoot._ExitTree()` disconnects any subscriptions already established and
disposes the lifecycle controller when one was created.

This division keeps long-lived composition and presentation objects out of
individual Level scenes. A Level contains its map and local gameplay content;
the Player is positioned in the same world coordinate space but is not a child
of the Level. The lifecycle controller can therefore replace only the active
Level while preserving the Player instance and its runtime state during a
portal transition. Save restoration can refresh that existing Player or create
one if the controller does not currently own one.

### Level content, exits, and replacement

Each `Level` validates its required spawn marker, TileMapLayer, bounds provider,
TileSet, calculated bounds, and authored transitions before it is committed.
It builds a `StringName`-keyed exit registry from descendant
`LevelTransition` nodes. Exit names identify arrival points; each transition
provides a Player origin marker and facing direction.

`LevelDefinition` and `LevelCatalog` are authored `Resource` data, not runtime
nodes. Each definition pairs the stable Level ID used by startup/save data with
an exported scene-resource reference string. `LevelRegistry`, created by
`WorldRoot` from the exported catalog, loads each reference as a `PackedScene`
and creates a `ResolvedLevelDefinition` containing both the authored definition
and its loaded scene. This resolved lookup is used by startup, portal changes,
and save restoration. Keeping the PackedScene reference as a string in the
authored resource avoids a direct scene-resource dependency between connected
Level definitions. The authored Level subclasses (`Playground` and `Grassland`)
are thin scene identities; shared validation and transition behavior live in
`Level`.

`LevelTransition` is an `Area2D` with an Inspector-selected target
`LevelDefinition`, source and target exit names, arrival marker/facing, and a
grid-relative collision shape. It emits the target definition's stable ID with
the request; `WorldRoot` resolves that ID through `LevelRegistry`. A Level
activates/deactivates its transition areas as a group, connects their request
events, and relays requests to the lifecycle controller. `WorldRoot` owns
request deduplication and the fade overlay; `LevelLifecycleController` owns
Level replacement.

Level replacement uses a prepare/commit shape. The controller instantiates and
validates a candidate Level and stages it before removing the current Level.
For portal arrivals, it resolves the target exit, moves the persistent Player
to that origin, updates facing and camera bounds, resets transient Player
state, activates the destination's exits, then queues the previous Level for
deletion. Restore uses the same Level preparation but applies saved Player
state through a separate restore entry point.

The normal portal request travels through these owners:

```text
LevelTransition detects Player body entry
  → LevelTransitionTriggered(target Level ID, target exit)
  → Level relays LevelTransitionRequested
  → LevelLifecycleController relays LevelTransitionRequested
  → WorldRoot rejects duplicate pending requests and defers handling
  → resolve Level ID, fade out, call ChangeLevel(), fade in
```

The controller prepares the candidate before committing it. Preparation
instantiates the PackedScene, calls `Level.TryPrepare()`, disables its
transitions, and stages it under `World/Levels` so child transforms and arrival
markers can be resolved. A successful portal commit deactivates and detaches
the previous Level, makes the candidate current, relocates and resets the
persistent Player, applies the candidate's camera bounds, subscribes and
activates the new Level's transitions, then queues the old Level for deletion.
If preparation or target-exit lookup fails, the candidate is discarded and
the active world remains in place.

After arrival, the destination transition remains suppressed until the Player
leaves its Area2D, preventing an immediate reverse transition.

`LevelBoundsProvider` derives a world-space `Rect2I` from used cells, TileSet
tile size, and the TileMapLayer's world-space origin. The lifecycle controller
passes the result to `Player.SetLevelBounds()`; `PlayerCamera` stores it before
entering the tree or applies it immediately when already ready. The legacy
bounds singleton is not part of this active path.

The bounds provider calculates a value and does not publish global state.
This keeps candidate-Level preparation from changing the current camera
before the candidate has been accepted. `Level.TryPrepare()` validates the
calculation; the controller supplies prepared bounds to the Player camera at
the point appropriate to the operation: before adding a new Player, or after
relocating an already-ready Player.

`LevelLifecycleController.CurrentLevelId` exposes the active Level's stable
definition ID, which is also the identity recorded in saves and used by
`WorldRoot`'s startup/load messages. The authored WorldRoot node name is
`WorldRoot`, so its log prefix uses that name. `Node.Name` remains scene-tree
metadata; it is not used as the Level's persistent identity.

### Game session, snapshots, and save/load

| Layer | Types | Responsibility |
| --- | --- | --- |
| Runtime progression | `GameSession`, `LevelSession` | Per-Level persistent string flags and deep-copy/commit operations. |
| In-memory world state | `WorldSnapshot`, `PlayerSnapshot`, `GameSnapshot`, `PlayerSpec` | Decouple gameplay nodes from JSON DTOs and restoration inputs. |
| Persistence DTOs | `GameSave`, `PlayerSave`, `LevelSave` | Store schema version, current Level ID, Player state, and per-Level flags. |
| Snapshot boundary | `IGameSnapshotPort`, `GameSnapshotAdapter` | Capture/restore game state while hiding scene and node details from the save service. |
| Persistence orchestration | `GameSaveService` | Validate DTOs, map snapshots to/from DTOs, and coordinate repository and world restoration. |
| File storage | `ISaveRepository`, `SaveRepository` | Read/write JSON saves under `user://saves/`. |

```text
PauseMenu Save → WorldRoot.OnGameSaveRequested() → GameSaveService
  → IGameSnapshotPort.TryCapture → GameSnapshotAdapter
  → map snapshot/session to GameSave → SaveRepository JSON

PauseMenu Load → WorldRoot.OnGameLoadRequested()
  → reject overlapping load/transition; set the shared loading guard
  → fade out and hide PauseMenu → WorldRoot.TryContinue(slot)
  → GameSaveService loads and validates GameSave
  → maps DTOs to GameSnapshot
  → GameSnapshotAdapter resolves Level ID and builds PlayerSpec
  → LevelLifecycleController.TryRestoreLevel()
  → commit GameSession progression and rebind PlayerHud on success
  → resume and fade in; on failure, fade in and show PauseMenu
```

`WorldStartupRequest` also represents `NewGame` and `LoadGame` startup modes.
`Main` currently selects `NewGame`; in-game loading uses the PauseMenu path
above. A title-screen caller can later provide `LoadGame` before the WorldRoot
enters the SceneTree.

The current Player save records health, maximum health, life state, global
position, and facing. The current default slot is `Save-01`. `Main` currently
selects `NewGame`; a title screen, continue selection, and save-slot selection
are not implemented. Per-Level flags are serialized, but `GameSnapshotAdapter`
does not yet apply them to Level-owned objects. Enemy/prop persistence is also
not implemented.

The save boundary keeps gameplay nodes out of the JSON layer. The snapshot
adapter reads the current Level ID and Player properties into an in-memory
`WorldSnapshot`/`PlayerSnapshot`; `GameSaveService` combines that snapshot with
a detached copy of `GameSession` progression and maps it to versioned DTOs.
During loading, repository data is validated and mapped back into a
`GameSnapshot`. The snapshot adapter resolves the stable Level ID through
`LevelRegistry`, assembles a `PlayerSpec`, and delegates the world operation
to `LevelLifecycleController`. The controller prepares the destination Level
and restores the Player before committing the Level change. Only after that
restore succeeds does `GameSaveService` replace the live session progression
with the loaded copy.

`SaveRepository` currently owns JSON serialization with
`System.Text.Json` and file access under `user://saves/`; the repository
interface leaves storage replaceable. The in-memory `GameSession` stores a
dictionary of `LevelSession` values, each of which currently contains a set of
persistent string flags. `GameSnapshot` deep-copies the session so a proposed
restore can travel independently of the active session until commit.

### Pause menu and persistent UI

`PauseMenu` emits Resume, Save, Load, and Quit requests. `MenuController` owns
`SceneTree.Paused` and menu visibility. `WorldRoot` routes save/load requests
and owns one loading guard shared by in-game loads and portal transitions. An
in-game load fades out, hides the menu, restores through `TryContinue()`, then
resumes and fades in on success; on failure it fades back in and shows the
menu. The HUD binds to the Player after initialization or successful loading.
`LevelTransitionOverlay` can process while paused and sequences fades around
Level changes and in-game loading.

`PlayerHud` is a persistent `CanvasLayer` with a `Container/HealthBar`
`HFlowContainer`. It owns the runtime `HealthSlot` instances, while Player
remains the source of truth for health and maximum health. `Bind()` first
detaches from any previous Player, then subscribes to `HealthChanged` and
`MaxHealthChanged`, synchronizes the number of slots, paints the current value,
and makes the HUD visible. This initial paint matters because Player health is
initialized in `Player._Ready()` before `WorldRoot.TryStart()` binds the HUD.

The current slot calculation uses `ceil(health)` and one slot represents one
health point. `HealthSlot.Value` maps `0`, `1`, and `2` to empty, half, and full
sprite frames, so fractional health is shown in half-heart increments. A
maximum health of six therefore produces six slots. On a maximum-health event,
the HUD creates or queues excess slots for deletion; on a health event, it
updates the frames of the existing slots.

`PauseMenu` is also persistent under `HUD`. Its button callbacks emit requests;
they do not directly own save storage or Level replacement. `MenuController`
receives the pause menu and the `SceneTree`, so pause state and menu visibility
are changed together. The transition overlay awaits each fade animation,
allowing `WorldRoot` to sequence Level replacement or save restoration between
the fades.

## Player and State Machine

The Player scene owns its movement collider, visual and interaction roots,
combat areas, animations/audio, camera, timer, and state nodes:

```text
Player (CharacterBody2D; player/Player.cs)
├── VisualRoot (Node2D)
│   ├── BodySprite (Sprite2D)
│   │   └── ShadowSprite (Sprite2D)
│   └── SwordFxSprite (Sprite2D)
├── InteractionRoot (Node2D)
│   ├── HitBox (Area2D scene)
│   │   └── CollisionShape2D
│   └── HurtBox (Area2D scene)
│       └── CollisionShape2D
├── CollisionShape2D
├── AnimationPlayer
├── PlayerStateMachine
│   ├── IdleState
│   ├── WalkState
│   ├── AttackState
│   ├── HitState
│   └── DeathState
├── PlayerCamera
├── Audio (Node2D)
│   ├── SwordSfx
│   └── HitSfx
├── FxAnimationPlayer
└── InvulnerabilityTimer
```

`Player` owns input sampling, facing, health/life and invulnerability, visual
transforms, animation playback, attack-window methods, and `MoveAndSlide()`.
`PlayerStateMachine` owns state registration, current/previous state, and
transitions. Each state owns its behavior and requests transitions through the
machine; states access the Player through the machine's injected context.

The Player initializes its dependencies and events before initializing the
machine. The machine registers its direct child states by concrete type,
injects itself into them, chooses the exported initial state or the registered
Idle fallback, and enters that state. Godot automatically invokes the
machine's callbacks; they are not called manually from Player. Its physics
priority is `-1`, so active-state physics updates velocity before Player's
default-priority `_PhysicsProcess()` calls `MoveAndSlide()`.

The initialization order in `Player._Ready()` is deliberate: mandatory child
references are resolved first; HitBox and HurtBox receive their owning Player;
health and timer callbacks are initialized; animation completion is connected;
facing transforms are applied; and the state machine is initialized last.
States can therefore use Player's animation, combat, health, and event
interfaces as soon as their initial `Enter()` runs. The machine starts with
processing disabled in its own `_Ready()` and enables inherited processing
only after state registration and initial-state entry are complete.

The Player samples movement input in `_Process()`. The active state's normal
`Process()` handles frame-rate behavior such as changing facing and refreshing
the walk animation. The state machine's earlier physics callback updates
velocity; Player's later physics callback performs `MoveAndSlide()`. Input
events such as attack are routed through `PlayerStateMachine._UnhandledInput()`
to the active state's `ProcessInput()`.

```text
Frame:   Player._Process reads InputDirection → machine forwards to active state
Physics: machine (-1) forwards active-state PhysicsProcess → Player (0) moves
Input:   machine._UnhandledInput forwards the event to active state
```

State changes are performed by the machine in a consistent order: the current
state exits, the machine remembers it when it is not marked as an interrupt
state, the new state becomes current, any supplied configuration callback
runs, then the new state enters. The previous-state reference lets HitState
resume the interrupted Idle, Walk, or other non-interrupt state. `AllowsReentry`
lets HitState restart when another hit arrives while it is already active.

| State | Entry and active behavior | Exit or transition |
| --- | --- | --- |
| Idle | Clears velocity and plays the idle animation. | Directional input enters Walk; attack input enters Attack. |
| Walk | Updates facing from movement input, applies `WalkSpeed`, and updates the walk animation when facing changes. | No movement returns to Idle; attack input enters Attack. |
| Attack | Subscribes to Player animation completion, plays and stores the exact directional attack key, and damps existing velocity. | Matching animation completion chooses Idle or Walk from current input; Exit closes the HitBox attack window and removes its temporary event subscription. |
| Hit | Requires configured `HitData`, applies knockback away from the hit origin, starts invulnerability, plays hit FX/animation, and damps velocity. | Matching hit animation completion resumes the previous non-interrupt state; Exit clears hit data and its event subscription. Re-entry is allowed. |
| Death | Requires configured fatal `HitData`, clears velocity, and currently uses the hit animation as a placeholder. | Matching animation completion calls Player's immediate `CompleteDeath()`/`Respawn()` path. |

`Player.PlayAnimation(PlayerAnimation)` constructs the directional animation key
and returns the exact key played. Left/right share the side animation while
`VisualRoot` and `InteractionRoot` are flipped together. The main
`AnimationPlayer` owns body frames and the sword effect's frames, position,
visibility/layering, sound playback, and HitBox position. The separate
`FxAnimationPlayer` controls hit effects, and Player owns the animation API
that state classes call. Player translates Godot's `AnimationFinished` signal
into `PlayerAnimationFinished`; states compare the reported key against the
exact key they started, so unrelated or superseded animations do not end the
state.

The authored method track calls `TriggerAttackSfx()` at 0.08 s,
`OpenAttackWindow()` at 0.10 s, and `CloseAttackWindow()` at 0.25 s. These
scene tracks are part of the Player-to-animation contract. The attack lasts
0.30 seconds in the current authored directional animations; the damage
window is shorter than the animation and the state remains responsible for
waiting until that animation finishes before returning to movement.

## Enemy and Combat Systems

The Slime scene is a `CharacterBody2D` with body/shadow sprites, movement
collider, animation player, state machine, shared HitBox/HurtBox scenes, death
effect, and hit audio. `Slime` owns health, facing, target reference, damage,
animation events, and `MoveAndSlide()`. Its `_Ready()` resolves and initializes
the combat components, establishes health and animation events, then
initializes the `EnemyStateMachine`. The machine follows the same scene-owned,
type-keyed pattern as the Player machine and updates velocity at physics
priority `-1` before Slime movement.

The Slime's state machine uses direct child state nodes and injects itself into
each state; each state reaches the Slime through that machine. Unlike the
Player's Idle/Walk input loop, Slime's Idle and Wander behaviors are timer
driven: Idle waits a configured random duration before entering its exported
next state, while Wander picks a cardinal direction, applies movement, plays
walk animation, and returns to its configured next state after a chosen number
of animation cycles. Hit is an interrupt state that resumes the previous
non-interrupt state after the hit animation. Death plays its death animation
and then queues the Slime for deletion.

| Enemy state | Entry and active behavior | Exit or transition |
| --- | --- | --- |
| Idle | Stops movement, plays idle, and counts a randomized duration when `_nextState` is configured. | Enters the exported next state when the timer expires. |
| Wander | Chooses one of the four cardinal vectors, updates facing, sets velocity, and plays walk. | Reapplies movement velocity in physics and enters its configured next state after the selected animation-cycle duration. |
| Hit | Requires configured `HitData`, applies knockback away from its origin, plays hit, and damps velocity. | Matching hit animation completion resumes the prior non-interrupt state; Exit clears data and unsubscribes. Re-entry is allowed. |
| Death | Requires fatal `HitData`, stops movement, and plays the death animation. | Matching animation completion calls `Slime.CompleteDeath()`, which queues the Slime for deletion. |

Combat separates body movement from detection. Player and Slime HitBoxes are
Area2D detectors; HurtBoxes receive damage. `HitBox` creates `HitData` with
damage, source identity/name, and origin, then calls `HurtBox.TakeHit()`. The
HurtBox synchronously emits its C# `Hit` event to the owning actor. Player,
Slime, and Plant own their respective health and hit response. Plant also has
a static blocking body. HitBox monitoring and HurtBox monitorability are
changed through deferred property updates when attack windows or invulnerability
change.

The Player's root `CharacterBody2D` collider handles physical movement and
environment collision. Its `InteractionRoot` contains the combat Areas and is
flipped with the visual root for left-facing attacks. The HitBox and HurtBox
are separate reusable scenes so attack detection and damage reception can be
composed onto other actors. HitBox monitoring determines whether it searches
for HurtBoxes; HurtBox monitorability determines whether another Area can
detect it. Both properties are changed with deferred updates because Godot
does not allow those changes immediately during an overlap signal.

Damage delivery is synchronous once an overlapping HitBox finds a HurtBox:

```text
Player/Slime HitBox.AreaEntered
  → HitBox captures damage, source instance/name, and source position in HitData
  → HurtBox.TakeHit(HitData)
  → HurtBox.Hit event
      ├── Player.HandleHit(): ignore while invulnerable/dead; subtract health;
      │   enter HitState or DeathState
      ├── Slime.HandleHit(): ignore if dead; subtract health;
      │   enter HitState or DeathState
      └── Plant.HandleHit(): subtract health and queue deletion at zero
```

The Plant is a damageable environment prop rather than an actor state machine;
its static body remains responsible for blocking movement. Slime contact
damage uses its always-enabled HitBox, while Player attack detection is opened
and closed by the authored attack-animation method tracks.

## Scene and Node Hierarchy

```text
Main (main.tscn; Node; application entry point)
└── ScreenHost (Node)
    └── WorldRoot (system/world_root.tscn; WorldRoot script; Node2D)
        ├── HUD
        │   ├── PlayerHud
        │   │   └── Container/HealthBar (runtime HealthSlot children)
        │   ├── LevelTransitionOverlay
        │   └── PauseMenu
        └── World
            ├── Levels (active/staged Level scenes)
            └── Player (persistent actor)

Level (Node2D; levels/Level.cs)
├── Grass (TileMapLayer scene; LevelBoundsProvider)
├── PlayerSpawn (Marker2D)
├── LevelTransitions (named LevelTransition areas)
└── Level content (Plants and Slimes in Playground)

Slime (CharacterBody2D; enemies/slime/Slime.cs)
├── ShadowSprite
├── BodySprite
├── CollisionShape2D
├── AnimationPlayer
├── EnemyStateMachine (Idle, Wander, Hit, Death)
├── HurtBox (shared combat scene)
├── HitBox (shared combat scene)
├── DeathFxSprite (with AnimationPlayer)
└── Audio/HitSfx
```

The Level subclasses `Playground` and `Grassland` currently provide scene
identity while shared Level behavior remains in `Level`. `LevelCatalog` and
`LevelDefinition` are authored resources rather than runtime nodes;
`ResolvedLevelDefinition` is the runtime pairing of one definition and its
loaded PackedScene.

The current Level content differs by scene. `Playground` contains Grass, two
Plant instances, two Slimes, a default Player spawn, and named North/East exit
areas. `Grassland` contains Grass, a default Player spawn, and a named West
exit. A Level's `LevelBoundsProvider` is the Grass TileMapLayer itself; the
same layer is supplied to each transition for grid-relative collision sizing
and editor snapping. This keeps map geometry, camera bounds, and portal sizing
anchored to the authored Level grid.

## Architecture Boundaries and Current Scope

- `Main` bootstraps a configured WorldRoot instance under `ScreenHost`;
  `WorldRoot` composes gameplay and owns persistent UI, while
  `LevelLifecycleController` owns active Level/Player references and world
  replacement.
- `LevelDefinition` and `LevelCatalog` describe authored Level content;
  `LevelRegistry` loads each referenced PackedScene and provides stable-ID
  lookup as `ResolvedLevelDefinition` values for startup, transitions, and save
  restoration. `LevelDefinition.Id` is the stable Level identity; `Node.Name`
  is scene-tree metadata.
- Gameplay nodes own gameplay behavior and events. The snapshot adapter is the
  boundary between those nodes and persistence DTOs.
- The current save system has one fixed slot and saves Player/current-Level
  data plus a per-Level flag structure. Per-Level flags are not yet applied to
  Level-owned objects on restore; inventory, quests, enemies, and prop
  persistence are future extensions.
- `ISaveSerializer` and `GameSaveService.TryReadGame()` are not part of the
  active save flow; `SaveRepository` currently uses `System.Text.Json` directly.

## Major Class and Function Index

The index lists the primary public entry points and lifecycle methods used to
understand system ownership and normal call flow. Small conversion helpers and
individual state-internal callbacks are omitted unless they define a system
boundary.

### Composition and Level lifecycle

| Class | Function | Purpose |
| --- | --- | --- |
| `Main` | `_Ready()` | Creates startup collaborators, configures a WorldRoot instance before tree entry, and keeps or removes it based on startup outcome. |
| `WorldRoot` | `Configure(GameSaveService, GameSession, WorldStartupRequest)` | Receives startup dependencies before entering the SceneTree. |
|  | `_Ready()` | Validates scene configuration, creates runtime controllers, connects requests, and initializes the requested world. |
|  | `_ExitTree()` | Disconnects UI/Level handlers and disposes the lifecycle controller if initialized. |
|  | `_UnhandledInput(InputEvent)` | Routes `ui_cancel` to pause/resume. |
|  | `Initialize()` | Dispatches the startup request and records success or failure. |
|  | `TryPrepare(out error)` | Validates LevelCatalog, initial ID, and Player scene; creates/resolves LevelRegistry. |
|  | `TryStart(out error)` / `TryContinue(string, out error)` | Starts a new world or restores a saved world, then binds the Player HUD. |
|  | `PerformLevelChange(string, StringName)` | Resolves the target, fades out/in, and calls lifecycle replacement. |
|  | `HandleLevelTransitionRequested(...)` | Deduplicates and defers transition handling outside the overlap callback. |
|  | `OnGameSaveRequested()` / `OnGameLoadRequested()` | Routes PauseMenu requests to save/load operations. |
| `WorldStartupRequest` | `NewGame` / `LoadGame(string)` | Selects fresh-start or save-load startup behavior; `Main` currently passes `NewGame`. |
| `LevelLifecycleController` (`levels/controller/`) | `TryStartLevel(ResolvedLevelDefinition, out error)` | Prepares the initial Level and creates the persistent Player. |
|  | `TryRestoreLevel(ResolvedLevelDefinition, PlayerSpec, out error)` | Prepares a loaded Level and loads/restores Player state before committing it. |
|  | `ChangeLevel(ResolvedLevelDefinition, StringName)` | Prepares and commits a portal-driven Level replacement. |
|  | `TryPrepareLevel(...)` | Instantiates, validates, deactivates, and stages a candidate Level. |
|  | `TryInitializePlayer(...)` / `TryLoadPlayer(...)` / `TryRestorePlayer(...)` | Create a fresh Player or apply loaded data to a new/existing Player. |
|  | `Dispose()` | Disconnects the active Level relay and disables its transition subscriptions. |
| `Level` (`levels/Level.cs`) | `TryPrepare(out error)` | Validates required scene references/bounds and builds the named-exit registry. |
|  | `GetRequiredInitialPlayerOrigin()` / `TryGetPlayerOrigin(...)` | Supplies the default spawn or resolves a named transition arrival. |
|  | `GetRequiredLevelBounds()` / `TryGetLevelBounds(...)` | Gets bounds from the configured provider, with required and try-style forms. |
|  | `ActivateLevelTransitions()` / `DeactivateLevelTransitions()` | Connects/disconnects and enables/disables Level exits. |
|  | `SuppressTransitionForPlayerArrival(...)` | Manages the destination exit's arrival suppression. |
| `LevelTransition` (`levels/runtime/`) | `TryValidate(out error)` | Validates authored exit data and arrival origin. |
|  | `TryGetPlayerOrigin(...)` | Returns the marker position and resolved arrival facing. |
|  | `SuppressForPlayerArrival(...)` | Suppresses the exit while the arriving Player remains inside its Area2D. |
|  | `SetDetectionEnabled(bool)` | Defers Area2D monitoring changes. |
|  | `SnapToGrid()` | Editor action to align the transition area with its TileMapLayer grid. |
| `LevelRegistry` (`levels/registry/`) | `LevelRegistry(LevelCatalog)` | Validates definitions, loads their scene references, and builds the stable-ID lookup. |
|  | `TryResolveLevelById(string, ...)` | Resolves an ID to its `ResolvedLevelDefinition`. |
| `LevelBoundsProvider` (`levels/runtime/`) | `CalculateLevelBounds()` / `TryCalculateLevelBounds(...)` | Calculates world-space bounds from used TileMapLayer cells. |
| `LevelDefinition`, `LevelCatalog` (`levels/metadata/`) | — | Authored resource data: stable Level ID/scene-reference pair and catalog collection. |
| `ResolvedLevelDefinition` (`levels/registry/`) | — | Runtime record pairing an authored definition with its loaded PackedScene. |

### Player and enemy behavior

| Class | Function | Purpose |
| --- | --- | --- |
| `Player` | `_Ready()` | Resolves and initializes actor components, event handlers, and the state machine. |
|  | `_Process(double)` / `_PhysicsProcess(double)` | Samples movement input; moves the CharacterBody2D. |
|  | `ConfigureInitialFacingDirection(...)` / `SetFacingDirection(...)` / `TryUpdateFacingDirection()` | Configures facing before tree entry or updates facing and visual/interaction transforms at runtime. |
|  | `PlayAnimation(PlayerAnimation)` | Plays and returns the exact directional animation key. |
|  | `SetHealth(...)` / `TryUpdateMaxHealth(...)` | Updates health and notifies the HUD. |
|  | `SetLevelBounds(Rect2I)` | Passes bounds to PlayerCamera. |
|  | `FreezeVelocity()` / `ApplyVelocity(...)` / `DampVelocity(...)` | Shared movement primitives used by Player states. |
|  | `ResetState()` / `StartInvulnerability(...)` / `EndInvulnerability()` | Resets transient combat state or controls the damage window. |
|  | `OpenAttackWindow()` / `CloseAttackWindow()` / `TriggerAttackSfx()` | Animation-track callbacks for the attack timeline. |
|  | `HandleHit(...)` / `CompleteDeath()` / `Respawn()` | Applies incoming damage and completes the current immediate death/respawn path. |
| `PlayerCamera` | `SetLevelBounds(Rect2I)` | Stores bounds or applies camera limits when ready. |
|  | `_Ready()` | Applies bounds configured before the camera entered the scene tree. |
| `PlayerStateMachine` | `Initialize(Player)` | Registers/injects scene-owned states and enters the configured/fallback state. |
|  | `_Process(double)` / `_PhysicsProcess(double)` / `_UnhandledInput(...)` | Forwards Godot callbacks to the active state. |
|  | `ChangeState<T>(...)` / `ChangeState(PlayerState)` | Performs typed, configured, or instance-based state transitions. |
|  | `GetSpecifiedState<T>()` / `ResumePreviousState()` | Resolves a registered state or resumes the previous non-interrupt state. |
| `PlayerState` | `Initialize(PlayerStateMachine)` and virtual `Enter/Exit/Process/PhysicsProcess/ProcessInput` | Base state context and lifecycle hooks. |
| `IdleState`, `WalkState`, `AttackState`, `HitState`, `DeathState` | `Enter()` and relevant process hooks | Implement Player-specific behavior; Hit/Attack/Death also consume animation completion. |
| `Slime` | `_Ready()` / `_PhysicsProcess(double)` | Initializes combat and enemy state dependencies; moves the CharacterBody2D. |
|  | `Initialize(Node2D)` / `SetTarget(Node2D?)` | Supplies the optional target reference. |
|  | `TryUpdateFacingDirection(...)` / `PlayAnimation(...)` | Updates facing and plays a directional animation. |
|  | `HandleHit(...)` / `CompleteDeath()` | Applies damage and queues the Slime for deletion after death behavior. |
| `EnemyStateMachine` | `Initialize(Slime)` / callback overrides | Registers states, injects the Slime, and forwards process/physics callbacks. |
|  | `ChangeState<T>(...)` / `ResumePreviousState()` | Changes or resumes scene-owned enemy states. |
| `EnemyState` | `Initialize(EnemyStateMachine)` and virtual `Enter/Exit/Process/PhysicsProcess` | Base enemy-state context and lifecycle hooks. |
| `IdleState`, `WanderState`, `HitState`, `DeathState` | `Enter()` and relevant process hooks | Implement Slime behavior and animation-driven transitions. |

### Combat, UI, and persistence

| Class | Function | Purpose |
| --- | --- | --- |
| `HitBox` | `Initialize(Node2D, float?)` / `SetEnabled(bool)` | Sets source/damage and toggles attack detection. |
|  | `OnAreaEntered(...)` / `LandHit(...)` | Builds HitData and delivers it to a HurtBox. |
| `HurtBox` | `Initialize(Node2D)` / `SetEnabled(bool)` / `TakeHit(HitData)` | Stores its victim, controls monitorability, and emits received-hit data. |
| `Plant` | `_Ready()` / `HandleHit(HitData)` | Initializes its HurtBox and handles damage/destruction. |
| `PauseMenu` | `ShowMenu()` / `HideMenu()` | Controls menu visibility; button callbacks emit user requests. |
| `MenuController` | `ToggleGamePause()` / `PauseGame()` / `ResumeGame()` / `QuitGame()` | Owns SceneTree pause state and menu visibility. |
| `PlayerHud` | `Bind(Player)` / `Unbind()` | Subscribes to Player health events and manages runtime health slots. |
|  | `SyncHealthSlots(...)` / `RenderHealthSlots(...)` | Sizes and paints the health display. |
| `HealthSlot` | `Value` | Maps an empty/half/full value to its sprite frame. |
| `LevelTransitionOverlay` | `FadeOut()` / `FadeIn()` | Plays and awaits the transition animations. |
| `GameSaveService` | `TrySaveGame(...)` / `TryLoadGame(...)` | Coordinates capture, validation, DTO mapping, repository I/O, and restore. |
|  | `TryReadGame(...)` | Reserved read API; currently returns false without reading. |
| `SaveRepository` | `TrySave(...)` / `TryLoad(...)` | Serializes/deserializes JSON files in the Godot user save directory. |
| `GameSnapshotAdapter` | `TryCapture(...)` / `TryRestore(...)` | Maps between live Level/Player nodes and in-memory snapshots. |
| `IGameSnapshotPort` | `TryCapture(...)` / `TryRestore(...)` | Defines the save service's engine-independent snapshot boundary. |
| `GameSession` | `DeepCopy()` / `RetainProgressionFrom(...)` | Copies and commits per-Level progression. |
| `LevelSession` | `DeepCopy()` | Copies a Level's persistent flags. |
| `GameComposition` | `CreateGameSaveService()` | Wires `SaveRepository` into `GameSaveService`. |
| `GameSnapshot`, `WorldSnapshot`, `PlayerSnapshot`, `PlayerSpec`, `GameSave`, `PlayerSave`, `LevelSave` | — | Data records/DTOs; they carry state and do not own scene behavior. |

