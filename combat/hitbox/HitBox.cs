using DAARPG.combat.hurtbox;
using Godot;

namespace DAARPG.combat.hitbox;

public partial class HitBox : Area2D
{
    [Export]
    public float DefaultDamage { get; private set; } = 1.0f;

    // private CollisionShape2D _collisionShape = null!;
    private float _damage;

    private Node2D Source { get; set; } = null!;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        // _collisionShape = GetNode<CollisionShape2D>("CollisionShape2D");
        AreaEntered += OnAreaEntered;
    }

    public void Initialize(Node2D source, float? damage = null)
    {
        Source = source;
        _damage = damage ?? DefaultDamage;
    }

    public void SetEnabled(bool enabled)
    {
        // _collisionShape.SetDeferred(
        // 	CollisionShape2D.PropertyName.Disabled,
        // 	!enabled);
        SetDeferred(Area2D.PropertyName.Monitoring, enabled);
    }

    private void LandHit(HurtBox hurtbox)
    {
        var hitData = new HitData(
            _damage,
            Source.GetInstanceId(),
            Source.Name,
            Source.GlobalPosition);
        hurtbox.TakeHit(hitData);
    }

    private void OnAreaEntered(Area2D area)
    {
        if (area is HurtBox hurtbox)
        {
            LandHit(hurtbox);
        }
    }
}
