using System;
using Godot;

namespace DAARPG.combat.hurtbox;

public partial class HurtBox : Area2D
{
    public Node2D Victim { get; private set; } = null!;

    // [Signal]
    // public delegate void HitEventHandler(float damage);
    public event Action<HitData>? Hit;

    public void Initialize(Node2D victim)
    {
        Victim = victim;
    }

    public void SetEnabled(bool enabled)
    {
        // _collisionShape.SetDeferred(
        // 	CollisionShape2D.PropertyName.Disabled,
        // 	!enabled);
        SetDeferred(Area2D.PropertyName.Monitorable, enabled);
    }

    public void TakeHit(HitData hitData)
    {
        // EmitSignal(SignalName.Hit, damage);
        Hit?.Invoke(hitData);
    }
}
