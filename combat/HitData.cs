using Godot;

namespace DAARPG.combat;

public readonly record struct HitData(
    float Damage,
    ulong SourceId,
    StringName SourceName,
    Vector2 HitOrigin
);

// public partial class HitData : RefCounted
// {
//     public float Damage { get; }
//     public Node2D Source { get; }
//
//     public HitData(float damage, Node2D source)
//     {
//         Damage = damage;
//         Source = source;
//     }
// }
