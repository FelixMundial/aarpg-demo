# Handoff: Phase 3 --- Serializer and Persistence Strategy Support

## Purpose

Use this chat to plan and implement the Phase 3 investigation of
alternative serialization formats and persistence backends for the
reusable Godot C# RPG template.

This task is **deferred until after Phase 1.5E (`Main` / `ScreenHost`
integration)**. Do not start this work early or combine it with the
application-root refactor. The current project architecture is already
documented in the project sources; consult that document for the full
class map and current implementation details rather than duplicating it
here.

## Current baseline

-   The game uses `GameSaveService` for save/load orchestration and
    validation.
-   `ISaveRepository` is the persistence boundary; `SaveRepository`
    currently reads/writes JSON under Godot's `user://saves/`.
-   `System.Text.Json` currently handles serialization/deserialization
    inside `SaveRepository`.
-   The initial save schema uses `GameSave.SchemaVersion = 1`.
-   `GameSave`, `PlayerSave`, and `LevelSave` are persistence DTOs.
-   `GameSession` is the active playthrough's persistent progression
    model; `LevelSession` currently stores per-Level persistent string
    flags.
-   `WorldSnapshot` captures the current Level and Player runtime state.
    `GameSnapshot` combines the world snapshot with a detached/deep copy
    of `GameSession`.
-   `LevelDefinition`, `LevelCatalog`, and `LevelRegistry` provide
    stable, human-readable Level IDs and resolve them to
    `ResolvedLevelDefinition` values.
-   `GameSaveService` maps between snapshots and save DTOs, validates
    data, and coordinates the repository and world-restoration port.
-   `GameSnapshotAdapter` (or the current world-snapshot adapter
    name in the architecture document) bridges Godot runtime objects and
    snapshot values.
-   There are no released or user-created save files that require
    migration. Preserve the current behavior until tests establish a
    baseline, but do not build a migration framework solely for
    hypothetical legacy data.
-   The active game has only minimal Player/Level persistence.
    Inventory, skills, party, quests, enemies, props, and rich character
    progression are future extensions.

## Goals

1.  Compare serializer choices on the same representative `GameSave`
    contract:
    -   JSON using `System.Text.Json` (current baseline)
    -   MessagePack
    -   Protocol Buffers / protobuf
    -   Godot Resource serialization and/or binary Variant serialization
2.  Separately evaluate storage backends:
    -   File-based repository (current)
    -   SQL-backed persistence, if justified by querying, indexing,
        transactions, or save-data scale
3.  Decide whether a serializer abstraction is warranted and, if so,
    introduce the smallest useful contract.
4.  Preserve separation between application use cases, snapshot mapping,
    serialization, and physical storage.
5.  Record measurements, trade-offs, dependency costs, and a
    recommendation appropriate for a reusable single-player RPG
    template.

## Non-goals

-   Do not implement the serializer experiments before Phase 1.5E is
    complete.
-   Do not change gameplay, Level transitions, Player ownership, or
    scene composition as part of this task.
-   Do not introduce a general-purpose DI container solely for
    persistence.
-   Do not assume SQL is a serializer; it is a storage/query technology
    and should be compared at the repository/backend layer.
-   Do not add fields for future gameplay systems until the state model
    actually needs them.
-   Do not create schema migrations for versions that have never been
    released. Design a migration path when a real incompatible schema
    change is introduced.

## Architectural boundaries to preserve

The application service should operate on snapshots and session state,
not live Godot Nodes or raw JSON.

Conceptual data flow:

``` text
GameSaveService
    |
    +-- world-snapshot port / Godot adapter
    +-- GameSession
    |
    v
GameSnapshot
    |
    v
GameSave (versioned persistence DTO)
    |
    v
ISaveRepository
    |
    v
File or database backend
```

The current `ISaveRepository` boundary should remain replaceable.
Serializer-specific code should not leak into `GameSaveService`,
`GameSession`, `GameSnapshot`, `LevelLifecycleController`, or
`WorldRoot`.

### Proposed abstraction to evaluate, not assume

A possible future design is:

``` csharp
public interface ISaveSerializer
{
    byte[] Serialize<T>(T value);
    T? Deserialize<T>(ReadOnlySpan<byte> data);
}

public interface ISaveRepository
{
    Task<RepositoryResult> SaveAsync(
        string slotId,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken);

    Task<RepositoryResult<byte[]>> LoadAsync(
        string slotId,
        CancellationToken cancellationToken);
}
```

These signatures are illustrative, not approved API contracts. Consider
whether synchronous APIs are sufficient for small files and whether an
abstraction over raw bytes is appropriate for Godot-native
serialization. Avoid adding `ISaveSerializer` unless at least two real
implementations or a testable reason justify it. If the repository
continues to own both serialization and file access, document that as a
deliberate simpler design rather than layering interfaces without need.

A database-backed implementation may require a different repository API
for transactions and queries. Do not force it into a byte-payload
abstraction if doing so makes SQL semantics awkward.

## Evaluation criteria

For each serializer/backend combination, record:

-   Correctness: round-trip all supported fields without loss or
    semantic changes.
-   Serialized size for identical sample state.
-   Serialize/deserialize time on the same target hardware and build
    configuration.
-   Compatibility and schema evolution: adding, removing, renaming, and
    defaulting fields.
-   Error behavior for truncated, malformed, unknown-version, and
    invalid data.
-   Human inspectability and ease of debugging.
-   Support for stable Level IDs and nested collections.
-   Cross-platform support and Godot/.NET integration.
-   External package maintenance, licensing, security posture, and
    upgrade cost.
-   Whether the format can represent the current DTOs without coupling
    the domain model to the serializer.
-   Operational complexity for a small single-player game.

Do not make a decision from microbenchmarks alone. Correctness,
maintainability, compatibility and developer experience are primary
criteria.

## Test data and test matrix

Use the same representative data for every implementation:

1.  Current minimal save: schema version, current Level ID, Player
    health/max health/life state/position/facing, and several Level
    flags.
2.  A larger synthetic save representing future growth: several
    characters, inventory item instances, equipped item IDs, learned
    skills, party lineup, quest flags, relationships, and multiple
    Levels. Clearly label this as synthetic test data, not implemented
    gameplay.
3.  Empty optional collections and absent optional fields where the
    format permits them.
4.  Invalid or missing required fields.
5.  Unknown Level ID.
6.  Unsupported schema version.
7.  Truncated/corrupt payload.
8.  Round-trip of strings with non-ASCII text.
9.  Deterministic comparison where applicable.
10. Repeated save/load and overwrite of an existing slot.

The schema-v1 test must remain representative of the current project. Do
not make synthetic future fields part of the production DTO just to
benchmark them.

## Serializer-specific investigation

### JSON / System.Text.Json

-   Use the current implementation as the baseline.
-   Verify options for naming, enum representation, missing fields,
    nulls, and strict validation.
-   Keep schema validation explicit; successful deserialization alone
    does not mean a save is valid.
-   Human readability is a significant debugging benefit.

### MessagePack

-   Select a maintained C# implementation and verify licensing,
    AOT/platform requirements, and Godot .NET compatibility.
-   Decide whether integer-keyed contracts or string keys are more
    maintainable.
-   Test schema evolution and unknown/missing fields.
-   Keep serialization contracts separate from domain classes where
    practical.

### Protocol Buffers

-   Evaluate schema-first `.proto` contracts, generated C# types,
    field-number stability, and deployment/build tooling.
-   Treat field numbers as permanent once released; never reuse removed
    field numbers.
-   Compare generated DTO mapping overhead against size/performance
    benefits.
-   Do not choose Protobuf simply because it is common in networked
    services; this is a local save system unless networking is added.

### Godot Resource / binary Variant

-   Verify what is actually serialized by the selected API and Godot
    version.
-   Evaluate Resource references, `Resource` identity/sharing behavior,
    custom Resource versioning, portability, and whether the format
    remains suitable outside Godot.
-   Distinguish Resource `.tres`/`.res` serialization from binary
    Variant encoding; they are not the same persistence contract.
-   Determine how stable logical Level IDs and content references
    survive resource renames.
-   Assess ease of inspecting and repairing corrupted saves.

## Storage-backend investigation

### File repository

Retain the current file repository as the baseline. Consider atomic
replacement (write temporary file then replace), backup behavior, slot
naming, directory creation, and corruption handling. Keep the existing
`user://saves/` behavior unless testing identifies a defect.

### SQL

Evaluate only if a concrete requirement justifies it, such as querying
many saves, indexed progression data, transactions across records, or
substantial structured state. Compare SQLite or another suitable
embedded option against storing one versioned snapshot per file.

Consider schema management, transactional semantics, migration burden,
platform availability, and backup/export behavior. SQL does not remove
the need for DTO versioning or data validation.

## Schema versioning and migration

-   Keep `GameSave.SchemaVersion` explicit.
-   Validate schema version before mapping to runtime models.
-   Introduce a migration pipeline when a released save schema changes
    incompatibly.
-   Prefer explicit version-to-version transformations and tests.
-   Never silently reinterpret a field or Level ID with a different
    meaning.
-   If a stable Level ID changes after release, add a deliberate
    alias/rename mapping or migration rather than relying on scene
    paths.
-   Test migration independently from the chosen serializer.

## Work plan

1.  Read the current architecture document and inspect the actual
    `GameSaveService`, `GameSave`, `ISaveRepository`, and
    `SaveRepository` code.
2.  Establish automated round-trip and validation tests for the current
    JSON implementation.
3.  Define the representative production and synthetic benchmark
    datasets.
4.  Research maintained serializer libraries and verify their current
    .NET/Godot compatibility.
5.  Implement one alternative at a time behind the smallest justified
    boundary.
6.  Run identical correctness tests and benchmarks.
7.  Evaluate file storage separately from serializer choice.
8.  Write a decision record comparing options and explaining the
    recommended default.
9.  Keep JSON unless an alternative provides a meaningful, measured
    benefit that outweighs added complexity.
10. Update the architecture documentation with the actual chosen design,
    measurements, dependencies, and limitations.

## Acceptance criteria

-   The current JSON implementation has regression tests.
-   Each evaluated serializer can round-trip the supported production
    DTO contract or has a documented incompatibility.
-   Invalid payloads and unsupported schema versions fail predictably.
-   Results use comparable data and measurements.
-   Serializer and repository responsibilities are explicitly decided.
-   SQL is evaluated as a backend, not incorrectly treated as a
    serialization format.
-   No gameplay or Level-lifecycle behavior changes as a side effect.
-   The architecture document records the decision and rationale.
-   The implementation remains proportionate to a reusable single-player
    RPG template; the fastest or most compact format is not
    automatically the winner.
