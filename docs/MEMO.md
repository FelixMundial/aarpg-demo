# MEMO

```C#
public override void _Ready()
{
    Player player = GetNode<Player>("Player");

    foreach (Node node in GetTree().GetNodesInGroup("enemies"))
    {
        if (node is Enemy enemy)
            enemy.Initialize(player);
    }
}
```

// or

```C#
Enemy enemy = enemyScene.Instantiate<Enemy>();
AddChild(enemy);
enemy.Initialize(player);
```

## ChaseState

```C#
if (Enemy.Target is null)
        return;

    Vector2 direction =
        Enemy.GlobalPosition.DirectionTo(Enemy.Target.GlobalPosition);

    Enemy.Velocity = direction * _chaseSpeed;
 ```

## Transaction in level transitions

Solution 1: Explicit preparation before scene-tree attachment.
Introduce a Level.ValidateConfiguration() or Prepare() method which checks exported references and builds the entrance registry without depending on _Ready().
This would allow early validation of the entrance IDs and the presence of required markers.
Global positions still need to be treated carefully because the detached scene may not yet have its final parent transform.
This is the strongest long-term design, but it requires reorganizing the current Level initialization.

## Character Progress

```C#
public sealed class CharacterProgress
{
    public HashSet<string> LearnedSkillIds
        { get; } = new();

    public Dictionary<EquipmentSlot, string> EquippedItemIds
        { get; } = new();
}

public enum EquipmentSlot
{
    MainHand,
    OffHand,
    Head,
    Body,
    Accessory
}

public sealed class PartyProgress
{
    public HashSet<string> RecruitedCharacterIds
        { get; } = new();

    public List<string> ActiveCharacterIds
        { get; } = new();
}

public sealed record RelationshipKey(
    string SourceCharacterId,
    string TargetCharacterId);

public sealed class RelationshipState
{
    public int Trust { get; set; }
    public int Affection { get; set; }
    public int Respect { get; set; }
}
```

If we later support different Player scenes for different character classes or playable protagonists, we can replace the single injected scene with a character-scene factory or resolver.

If later we support multiple playable characters, different Player scenes per class, or NPCs instantiated from different scene types, we can introduce a character-scene resolver

### 

Legacy: `LevelBoundsService` (Autoload Singleton)

**Original purpose.** `LevelBoundsService` was introduced to share the active Level's camera bounds between `GameRoot` and `PlayerCamera` without requiring direct references between their scenes. It also served as an example of the Singleton and Observer patterns in Godot C#.

**How it worked.** Registered as a Godot autoload, it exposed a static `Instance`, cached the current bounds as `Rect2I`, and published `MapBoundsChanged` events. `GameRoot` supplied bounds calculated by `LevelBoundsProvider` before Player initialization, allowing `PlayerCamera` to obtain them during `_Ready()` and respond to subsequent Level changes.

**Why it was replaced.** Map bounds belong to the active gameplay Level, not to the application globally. The singleton introduced a hidden dependency, global mutable state, and event-subscription management for a single camera consumer.

**Current approach.** `LevelLifecycleController` obtains the prepared Level's bounds and explicitly configures the Player's camera. `PlayerCamera` applies its initial limits during `_Ready()` and reapplies them when the active Level changes.

The legacy implementation is retained for educational reference, marked `[Obsolete]`, and removed from Godot's active autoload configuration.

### Legacy notes

For your current unscaled, unrotated tilemap, replace the return with:

```C#
var worldPosition = ToGlobal((Vector2)localPosition);
return new Rect2I(
    new Vector2I(
        Mathf.RoundToInt(worldPosition.X),
        Mathf.RoundToInt(worldPosition.Y)),
    localSize);
```

This corrects the limits’ location. To see camera following within limits, enlarge the map or zoom in so the visible area is smaller.