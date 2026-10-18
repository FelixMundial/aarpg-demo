# Player State Machine Overview (Godot C#)

Snapshot: 2026-10-05. Based on inspection of all C# scripts under `player/`
and `player/player.tscn`, plus the combat components and Plant receiver.
This describes the current implementation, not a proposed replacement. No build or runtime validation was performed for this
documentation update.

## Scene and Source Map

```text
Player (CharacterBody2D; player/Player.cs)
├── VisualRoot (Node2D)
│   ├── BodySprite
│   │   └── ShadowSprite
│   └── SwordFxSprite
├── InteractionRoot (Node2D)
│   └── HitBox (combat/hitbox/HitBox.tscn)
│       └── CollisionShape2D
├── CollisionShape2D
├── AnimationPlayer
├── PlayerStateMachine (player/state_machine/PlayerStateMachine.cs)
│   ├── IdleState
│   ├── WalkState
│   └── AttackState
├── Camera2D
└── Audio (Node2D)
    └── SwordSfx (AudioStreamPlayer2D)
```

State scripts, including abstract `PlayerState`, live in
`player/state_machine/states/`. Animation enums live in `player/enums/`.
The scene configures `_initialStatePath = NodePath("IdleState")`. Combat
components live in `combat/hitbox/`, `combat/hurtbox/`, and `combat/HitData.cs`.

## Ownership and Initialization

- `Player` owns input, facing, visual transforms, animation playback/events,
  attack sound playback, attack-window control, and `MoveAndSlide()`. Its
  single animator's scene tracks coordinate body frames, sword effects,
  sound timing, and hitbox activation.
- `PlayerStateMachine` owns state registration, active/previous state,
  transitions, and callback forwarding.
- States own behavior: when to change facing/animation, set velocity, and
  request transitions. They access `Player` through the injected machine.

The machine disables its Godot processing in `_Ready()`. `Player._Ready()`
first resolves VisualRoot, InteractionRoot, HitBox, animator, and machine.
It initializes HitBox with `this` as the damage source, disables its
monitoring, and subscribes to `AnimationPlayer.AnimationFinished`. It then
initializes the state machine last, so states enter with their dependencies
ready. Machine initialization:

1. Rejects repeated initialization with an error.
2. Stores the player and registers direct child `PlayerState` nodes in a
   `Dictionary<Type, PlayerState>`, injecting the machine into each state.
3. Selects the configured initial state and calls `Enter()`.
4. Sets `ProcessMode` to `Inherit` and marks the machine initialized.

Duplicate concrete state types report an error and are skipped. Initial-state
selection requires the exact registered instance; an unregistered resolved
state reports an error, while a missing path falls back to registered
`IdleState`. Missing requested types throw `InvalidOperationException`.

## Callbacks and Transitions

**Current preference:** the project owner temporarily prefers automatic
Godot callbacks. Keep this approach for now; the manual alternative below
is migration guidance, not a request to refactor the scripts.

Godot calls the machine's `_Process`, `_PhysicsProcess`, and
`_UnhandledInput` overrides, which forward to the active state. The manual
frame/physics calls in `Player` are commented out. Static inspection finds
no remaining manual-plus-automatic callback duplication.

```text
Frame (both priorities 0; parent before child):
  Godot → Player._Process → read Direction
  Godot → machine._Process → active state.Process
Physics (current configured priorities):
  Godot → machine._PhysicsProcess (-1) → active state.PhysicsProcess
  Godot → Player._PhysicsProcess (0) → MoveAndSlide
Input:
  Godot → machine._UnhandledInput → active state.ProcessInput
```

**Current ordering:** the machine sets `ProcessPhysicsPriority = -1` in
`_Ready()`, while Player retains the default physics priority `0`. Neither
node has a scene priority override. The machine therefore updates state
velocity before Player calls `MoveAndSlide()` in the same physics tick.
Frame priorities remain `0`; parent-before-child ordering lets Player sample
input before the machine's frame update. Static inspection confirms both
the duplicate manual calls and the earlier reversed physics priority have
been resolved; callback counts have not been measured at runtime.

See [Godot callback ordering](https://docs.godotengine.org/en/stable/classes/class_node.html#class-node-private-method-physics-process).

`PlayerState` provides empty virtual `Enter`, `Exit`, `Process`,
`PhysicsProcess`, and `ProcessInput` hooks. States override only needed hooks.
`ChangeState<T>()` resolves an existing scene-owned state by concrete type;
it does not instantiate nodes. A real transition calls old `Exit()`, stores
it as previous, assigns the new state, and calls new `Enter()`. Requests for
the current instance are ignored. Initial entry is handled separately.

`ResumePreviousState()` falls back to idle if no previous state exists.
The machine also provides `ChangeState(PlayerState)` and
`ChangeState<TTrue, TFalse>(bool)`; neither previous-state resumption nor the
conditional helper is used by the active attack-completion code.

## State Behavior

| State | On entry | While active | Exit trigger |
| --- | --- | --- | --- |
| Idle | Play idle animation | Clear velocity when direction is zero | Movement → Walk; attack input → Attack |
| Walk | Update facing and play walk animation | Refresh animation when facing changes; set velocity to `Direction * WalkSpeed` (default 100) | Zero direction → Idle; attack input → Attack |
| Attack | Subscribe to player animation event, then play attack and save its exact key | Damp existing velocity in physics; keep facing unchanged | Matching animation completion → Idle or Walk according to current direction |

`Direction` is sampled with `Input.GetVector("left", "right", "up", "down")`.
Attack input reaches idle/walk through `_UnhandledInput`; attack itself does
not override input handling. Attack unsubscribes from the event in `Exit()`.
Velocity is not set by state entry methods: walking applies it in physics,
and attack uses `Velocity *= Mathf.Exp(-_decelerationRate * (float)delta)`.
The exported rate defaults to `5.0` (inspector range `1–20`, step `0.5`),
with no override in the scene. This exponentially damps existing motion
rather than immediately stopping it; attack does not apply fresh movement
input. The linear `MoveToward` alternative remains commented out. State
entry does not immediately apply the new velocity; verify transition timing
separately from callback ordering.

## Animation Contract and Review Principles

`Player.PlayAnimation(PlayerAnimation)` builds and returns the full key:
`{idle|walk|attack}_{up|down|side}`. Left/right share `side`; player flips
`VisualRoot.Scale.X`, mirroring body, shadow, and sword effect together while
leaving the body collision, InteractionRoot, camera, and audio outside that
transform. States express
animation intent through enums rather than locating animation nodes.

Player relays the animator's completion signal through the C# event
`PlayerAnimationFinished`. Attack compares that event's `StringName` with
the exact key returned by playback, such as `attack_down`. The scene's three
attack animations last 0.3 seconds and are non-looping. Idle/walk animations
use ping-pong looping. The single `AnimationPlayer` now owns the full attack
timeline; there is no separate `SwordFxAnimationPlayer` or attack-specific
branch in `Player.PlayAnimation()`.

Each attack timeline animates body and sword frames, sword position and
layering (`show_behind_parent`), and sword visibility (`true` at time `0`,
`false` at `0.3`). Sword FX starts hidden; `RESET` also keys it hidden.
Idle/walk tracks animate only body frames, so early attack interruption would
need an explicit sword-visibility cleanup if that behavior is added later.

Each attack animation has an enabled method track targeting Player:

| Timeline time | Player method | Effect |
| --- | --- | --- |
| 0.08 s | `TriggerAttackFx()` | Resolve `Audio/SwordSfx`, randomize pitch from 0.9 to 1.1, and play sound |
| 0.10 s | `OpenAttackWindow()` | Call `HitBox.SetEnabled(true)` |
| 0.25 s | `CloseAttackWindow()` | Call `HitBox.SetEnabled(false)` |

The older audio `playing` property tracks remain disabled. Method names and
node paths are scene-to-C# contracts. These are authored timeline times;
actual animation/physics scheduling has not been measured in this update.

## Attack Interaction and Damage Flow

Visual facing and collision facing are separate: `TryUpdateFacingDirection()`
updates both roots when the cardinal direction changes. VisualRoot mirrors
left/right; InteractionRoot rotates the offset hitbox (position `(0, 15)`)
using Up = Pi, Left = Pi/2, Right = -Pi/2, Down = 0. The fallback branch now
sets `0f`, so returning to Down resets the hitbox rotation rather than
retaining the previous direction's angle.

The current scene starts facing Down with unrotated/unmirrored roots, which
matches the code's default. `_Ready()` still does not apply initial facing
transforms: if a future scene overrides the exported facing to Up/Left/Right,
initialize both transforms before entering the initial state. The current
default setup does not require that correction.

`HitBox` deals damage; `HurtBox` receives damage. The player's HitBox overrides
the reusable scene's mask to `256` (layer 9, Enemy), matching Plant's HurtBox
layer. HitBox has collision layer `0` and is not monitorable; HurtBox has
mask `0` and does not monitor. The player's body mask is `16` (layer 5,
Environment), separate from attack detection.

`HitBox.SetEnabled()` currently sets `Monitoring = enabled`; it does not
disable the collision shape. Shape caching and a deferred Disabled-property
alternative remain in the script, but the alternative is commented out.
On an entered HurtBox, the active damage flow is:

```text
HitBox.AreaEntered → LandHit → new HitData(Damage, Source)
  → HurtBox.TakeHit → print hit message → HitTaken.Invoke
    → Plant.HandleHitTaken → subtract health
      → if Health <= 0: print death message → QueueFree
```

`HitData` is a readonly record struct carrying damage and the source Node2D.
HitBox's exported damage defaults to 1. Plant initializes its HurtBox with
itself as Victim, subscribes to the C# event, and starts with default health
2. Plant's scene instances `combat/hurtbox/HurtBox.tscn` with its matching
UID. Event handlers run synchronously, so printing before invocation now
puts the hit message before any death message from that hit. `AttackData`
is also declared but is not used by the current damage flow.

AttackState still only unsubscribes from animation completion in `Exit()`;
closing the damage window is timeline-owned. If early interruption is added,
review hitbox and sword-visibility cleanup together. There is currently no
per-swing target registry or source/self filtering in HitBox; collision
layers and area-entry events determine which receivers are hit.

Preserve explicit ownership, scene-owned states, type-based lookup, and
state-scoped event subscriptions when extending this design. Inspect scripts
and the scene together; treat commented alternatives as historical, and
separate static observations from build/runtime verification.

## How to Change to Manual Callbacks (Future Option)

If the project owner later chooses Player-driven frame/physics updates:

1. Replace the machine's `_Process` and `_PhysicsProcess` overrides with
   ordinary public methods. Do not keep forwarding overrides alongside them:

   ```csharp
   public void Process(double delta) => _currentState.Process(delta);
   public void PhysicsProcess(double delta) => _currentState.PhysicsProcess(delta);
   ```

2. Keep the initialization gate (`ProcessMode = Disabled` in `_Ready()`,
   then `Inherit` after initialization). Optionally add `SetProcess(false)`
   and `SetPhysicsProcess(false)` in `_Ready()` to make the manual scheduling
   intent explicit. Removing the overrides already removes their automatic
   enablement. Keep `_UnhandledInput` as an override: input still comes from
   Godot. Leaving the whole machine disabled after initialization would also
   block that input callback.

3. In `Player._Process`, sample `Direction` first, then call
   `_playerStateMachine.Process(delta)`. In `Player._PhysicsProcess`, use:

   ```csharp
   _playerStateMachine.PhysicsProcess(delta);
   MoveAndSlide();
   ```

   Use ordinary method names rather than calling Godot callback overrides.
   Each update then has one caller, and ordering is explicit in Player.

4. Remove priorities used solely to order Player against the machine; they
   no longer control these manual calls. Preserve priorities needed for other
   systems. Direct C# calls do not consult the callee's process mode, so Player
   must call only after initialization and remain the pause-aware scheduler.

5. Verify one state frame update per frame and one state physics update per
   physics tick. Check walking start/stop, attack damping/completion, damage
   window timing, facing, input consumption by UI, and pause/resume. The migration does not change state
   registration, transitions, event subscriptions, or state-entry velocity.

See [Godot processing switches](https://docs.godotengine.org/en/stable/classes/class_node.html#class-node-method-set-process).
