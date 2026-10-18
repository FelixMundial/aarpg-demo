# Handoff: Asynchronous Loading Refactor

## Purpose

Use this chat to plan and implement a focused asynchronous-loading
refactor for the Godot C# RPG template.

This task is **deferred until Phase 3**, and should be handled in a
dedicated chat after Phase 1.5E is complete. Do not begin async changes
during the current Phase 1.5 work. The current project architecture is
already documented in the project sources; consult it for actual method
names, scene ownership, and current call flows.

The immediate goal is to enable non-blocking Level resource loading
while preserving the existing Level/Player lifecycle invariants. This is
not a mandate to make every method `async`.

## Current baseline

-   The project targets Godot .NET 4.7.2 and .NET 8 by default (the
    architecture document may record additional target-specific
    frameworks).
-   `LevelRegistry` maps stable human-readable `LevelId` values to
    `LevelDefinition` and `ResolvedLevelDefinition` values.
-   `LevelDefinition` stores a scene resource reference in a form
    intended to avoid circular dependencies; the exact current
    implementation must be inspected before changing it.
-   `LevelLifecycleController` owns the active Level and Player
    references, prepares destination Levels, restores Player state,
    commits replacements, manages transition subscriptions, and
    configures camera bounds.
-   `WorldRoot` coordinates startup, portal-transition requests, fades,
    PauseMenu input, and save/load requests.
-   `GameSaveService` orchestrates persistence, validation, DTO/snapshot
    mapping, repository access, and world restoration through an
    interface.
-   `SaveRepository` currently uses `System.Text.Json` and file access
    under Godot's `user://saves/`.
-   `GameSnapshotAdapter` (or the current adapter name in the
    architecture document) translates between Godot runtime objects and
    detached snapshots.
-   The current load flow is synchronous.
    `GameSaveService.TryReadGame()` was previously described as a
    reserved stub; inspect the current source before assuming it still
    exists.
-   The pause menu should remain paused after a failed load and resume
    only after successful completion.
-   The active Level and Player must not be left partially restored if
    candidate preparation fails.

## Why asynchronous loading is being deferred

The project currently has two small Levels. Synchronous loading is
adequate for now, and changing all save/load methods to `async` would
add complexity without necessarily improving responsiveness.

The refactor is deferred until Phase 3 so it can be planned alongside
persistence/serializer decisions, without destabilizing the Phase 1.5
application-root migration.

Do not interpret this deferral as an agreement to implement every
possible asynchronous API. The work should be justified by actual
operations that can yield.

## Distinguish three kinds of work

### 1. Asynchronous file I/O

Reading/writing save files may be asynchronous if the repository uses
.NET streams and APIs such as `System.Text.Json` asynchronous stream
methods.

The current save payload is small. Keep repository I/O synchronous
unless measurements or target requirements justify changing it. If
converting repository I/O, make the contract coherent (typically both
`SaveAsync` and `LoadAsync`) and propagate cancellation where
appropriate.

Godot's `user://` paths are not directly ordinary absolute filesystem
paths; use Godot's path API to obtain a filesystem path before opening a
.NET stream.

Do not wrap synchronous Godot `FileAccess` calls in `Task.Run()` and
label them asynchronous.

### 2. Threaded resource loading

This is the principal expected benefit.

The current registry may load each `PackedScene` while it builds its
lookup table. That can make the initial registry construction
synchronous and means a later `WorldRoot.Load()` cannot make that work
asynchronous after the fact.

The proposed change is for `LevelRegistry` to validate and index stable
IDs/definitions, then lazily resolve the scene resource when a Level is
requested.

Investigate Godot's supported threaded resource loading APIs,
especially:

-   `ResourceLoader.LoadThreadedRequest(...)`
-   `ResourceLoader.LoadThreadedGetStatus(...)`
-   `ResourceLoader.LoadThreadedGet(...)`

Poll the status across frames. Do not use a tight polling loop. Do not
call the blocking retrieval method before the resource is ready. Inspect
the exact signatures and restrictions for the project's Godot version
before implementing.

A successful resolution should return a `ResolvedLevelDefinition`
containing the validated `LevelDefinition` and loaded `PackedScene`.
Cache successful resources so later requests can reuse them.

Threaded resource loading does not mean scene-tree operations become
thread-safe. Keep Node instantiation where required, `_Ready`/scene
initialization, active SceneTree mutation, Player relocation, Level
commit, and cleanup on the appropriate Godot/main-thread execution path.

### 3. Asynchronous orchestration

`async`/`await` lets a caller yield while real asynchronous work is
pending. It does not automatically move synchronous code to a background
thread.

Do not add `async` to pure validation, snapshot mapping, or scene-commit
helpers unless they actually await an asynchronous operation.

C# async methods cannot use `ref`, `in`, or `out` parameters. Existing
`bool TryX(..., out string error)` contracts cannot simply be converted
to `async Task<bool>`.

Introduce a small result type for async operations, for example:

``` csharp
public sealed record OperationResult(bool Success, string? Error);

public sealed record OperationResult<T>(
    bool Success,
    T? Value,
    string? Error);
```

These are illustrative types. Choose nullability and generic result
semantics carefully, and avoid building a large generic result framework
for this one workflow.

## Desired end-to-end flow

``` text
WorldRoot receives Load request
    |
    v
GameSaveService.LoadGameAsync(slotId, ...)
    |
    +-- Load/validate GameSave
    +-- Map DTO to detached GameSnapshot
    |
    v
IWorldSnapshotPort.RestoreAsync(snapshot, ...)
    |
    v
Godot world-snapshot adapter
    |
    +-- LevelRegistry.ResolveLevelAsync(CurrentLevelId, ...)
    +-- Validate snapshot and build Player restore input
    |
    v
LevelLifecycleController.TryRestoreWorld(
    ResolvedLevelDefinition, PlayerRestoreSpec, ...)
    |
    +-- Prepare candidate Level
    +-- Prepare/restore Player
    +-- Apply candidate camera bounds
    +-- Commit Level and Player state
    +-- Retire old Level only after successful preparation
    |
    v
Commit copied GameSession progression
    |
    v
Return successful operation result
    |
    v
WorldRoot resumes game / closes PauseMenu
```

The actual method names should follow the current architecture document.
The flow above expresses responsibilities, not an instruction to create
duplicate APIs.

## Which methods should become asynchronous?

### `LevelRegistry`

Introduce an asynchronous resolution method, conceptually:

``` csharp
Task<OperationResult<ResolvedLevelDefinition>> ResolveLevelAsync(
    string levelId,
    CancellationToken cancellationToken);
```

This method should validate the logical ID, request threaded resource
loading, poll across frames, handle load failure/cancellation, and
return the resolved definition plus `PackedScene`.

Do not keep eagerly loading every Level scene in the registry
constructor if the objective is lazy loading.

### `WorldRoot.TryStart()` / startup

If initial Level resource resolution becomes asynchronous, startup must
await it. Godot `_Ready()` overrides cannot return `Task`; use a small
async orchestration method invoked by `_Ready()`, with explicit error
handling and cancellation/lifetime checks.

Ensure the application can report initial-load failure instead of
leaving a half-initialized WorldRoot.

### `WorldRoot.PerformLevelChange()`

Portal transitions must await destination resolution if the registry is
lazy/asynchronous. Preserve the existing pending-transition guard and
deferred handling of requests originating from physics callbacks.

Keep the fade overlay able to process while paused. If resolution or
preparation fails, restore a usable gameplay/menu state and do not leave
the screen permanently faded out.

### `WorldRoot.TryContinue()` / load request handler

The public loading workflow should return `Task` or `Task<T>`, not
`async void`. A Godot event handler may be `async void` because its
delegate signature requires `void`, but it should catch/report
exceptions and await a task-returning operation.

Resume the game only after successful restoration. A failed load should
not resume automatically.

### `GameSaveService.TryLoadGame()`

Rename to a task-returning API such as `LoadGameAsync(...)` if it awaits
repository I/O or the world snapshot port. It remains responsible for
validation, DTO/snapshot mapping, coordinating restoration, and
committing the candidate `GameSession` only after successful world
restoration.

Do not add `Task.Run()` around mapping or validation merely to make the
API appear asynchronous.

### `GameSnapshotAdapter.TryRestore()`

Make the operation task-returning if it awaits Level resolution. It
validates/converts snapshot values, resolves `CurrentLevelId` through
`LevelRegistry`, builds the appropriate Player restoration input, and
delegates scene operations to the lifecycle controller.

Use the current adapter/port naming in the architecture document unless
the dedicated chat establishes a justified rename.

### `SaveRepository.TryLoad()`

Keep synchronous initially unless genuine asynchronous file I/O is
selected. If changed, consider both `SaveAsync` and `LoadAsync`,
cancellation, `System.Text.Json` stream APIs, path conversion from
`user://`, and consistent error results.

Do not confuse async file I/O with threaded scene resource loading. They
are separate concerns and can be adopted independently.

### `LevelLifecycleController.TryRestoreWorld()`

Keep synchronous if it receives an already-resolved
`ResolvedLevelDefinition` and performs scene preparation/commit on the
main thread. It should not perform its own resource loading.

Only introduce an asynchronous lifecycle operation if the controller
later needs to await a genuine asynchronous step of its own. Do not
propagate `async` through it just for naming consistency.

## Cancellation, concurrency, and scene lifetime

The refactor must explicitly address:

-   A second Load request arriving while another load is pending.
-   Portal transitions overlapping loading.
-   `WorldRoot` leaving the SceneTree while a load is in progress.
-   Cancellation of a threaded resource request or cancellation of
    waiting for its result.
-   Whether a successfully loaded `PackedScene` should remain cached
    after a caller cancels.
-   Failure after resource loading but before Level commit.
-   Correct disposal of staged candidate nodes.
-   Unsubscribing old Level events and avoiding duplicate subscriptions.
-   The previous active world remaining usable until candidate
    preparation succeeds.
-   Preventing Player/HUD/session mutation from being committed before
    the world restore succeeds.

Use one authoritative lifecycle/busy-state policy. Do not create
unrelated busy flags in `WorldRoot`, the adapter, and the lifecycle
controller without defining their responsibilities.

Do not access or mutate active scene-tree Nodes from a background
thread. Resource loading may be threaded; scene-tree operations remain
coordinated on the appropriate Godot thread.

## Test plan

1.  Load the initial Level with threaded resolution and verify startup
    succeeds.
2.  Travel through both portal directions; confirm the destination Level
    and exit work correctly.
3.  Load a save from the same Level and from a different Level.
4.  Verify camera bounds and arrival-transition suppression.
5.  Request an unknown Level ID and a missing/corrupt scene resource.
6.  Cancel or exit while loading; ensure no orphan nodes or stale
    callbacks remain.
7.  Issue duplicate Load requests and overlapping portal/load requests;
    verify deterministic rejection or queuing.
8.  Confirm failed loads leave the current world and active
    `GameSession` intact during preparation failures.
9.  Confirm the PauseMenu remains paused after failure and resumes only
    after successful restoration.
10. Profile whether asynchronous file I/O is useful independently of
    threaded scene loading.
11. Test on the actual target platforms and Godot version.

## Work plan

1.  Read the current architecture document and inspect the actual
    registry, adapter, service, lifecycle controller, and WorldRoot
    implementations.
2.  Record the synchronous baseline and existing failure behavior.
3.  Refactor `LevelRegistry` to support lazy threaded scene resolution.
4.  Introduce the smallest useful async result/cancellation contract.
5.  Propagate asynchronous resolution through startup and portal
    transitions.
6.  Propagate it through snapshot restoration and `GameSaveService`.
7.  Decide separately whether repository file I/O should become
    asynchronous.
8.  Add concurrency/cancellation guards and test all failure paths.
9.  Update the architecture document with actual async contracts and
    thread-safety constraints.

## Acceptance criteria

-   Large Level resources can be requested without blocking the main
    loop while loading.
-   Resource-load status is polled across frames; no busy-wait loop or
    premature blocking retrieval is used.
-   Scene-tree mutation and Level commit remain on the appropriate Godot
    thread.
-   Initial startup, portal transitions, and save restoration all use
    the same Level resolution policy.
-   The existing synchronous lifecycle commit remains synchronous unless
    there is a demonstrated need to change it.
-   The save/load operation returns an awaitable result; errors are
    observable.
-   Cancellation, duplicate requests, scene exit, and failed resource
    resolution are handled without stale references or orphan nodes.
-   The active world/session is not committed to a failed candidate.
-   Pause/resume behavior is correct.
-   Async file I/O is either implemented with a measured rationale or
    explicitly left synchronous.
-   The project builds and regression tests pass on the target
    Godot/.NET configuration.
