using Godot;

namespace DAARPG.combat;

public readonly record struct AttackData(
    float Damage,
    Node2D Source
);
