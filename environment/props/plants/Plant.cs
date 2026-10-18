using DAARPG.combat;
using DAARPG.combat.hurtbox;
using Godot;

namespace DAARPG.environment.props.plants;

public partial class Plant : Node2D
{
    [Export]
    public float MaxHealth { get; private set; } = 2.0f;

    private HurtBox _hurtBox = null!;

    public float Health { get; private set; }

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        _hurtBox = GetNode<HurtBox>("HurtBox");
        _hurtBox.Initialize(this);
        _hurtBox.Hit += HandleHit;

        Health = MaxHealth;
    }

    public override void _ExitTree()
    {
        if (_hurtBox is { } hurtBox)
        {
            hurtBox.Hit -= HandleHit;
        }
    }

    private void HandleHit(HitData hitData)
    {
        Health -= hitData.Damage;

        if (!(Health <= 0))
        {
            return;
        }

        GD.Print($"{Name} was destroyed by {hitData.SourceName}.");
        QueueFree();
    }
}
