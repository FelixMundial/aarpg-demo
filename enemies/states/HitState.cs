using System;
using DAARPG.combat;
using DAARPG.player.enums;
using Godot;

namespace DAARPG.enemies.states;

public partial class HitState : EnemyState
{
    [Export(PropertyHint.Range, "1,20,0.5")]
    private float _decelerationRate = 5.0f;

    [Export(PropertyHint.Range, "0,400,10")]
    private float _knockbackSpeed = 200.0f;

    private StringName _activeHitAnimation = null!;

    private HitData? _hitData;

    public override bool AllowsReentry => true;

    public override bool IsInterruptState => true;

    public override void Enter()
    {
        if (_hitData is not { } hitData)
        {
            throw new InvalidOperationException(
                "HitState must be configured with HitData before Enter()."
            );
        }

        Enemy.EnemyAnimationFinished += OnHitAnimationFinished;

        // Incorrect knockback logic
        // ApplyVelocity(-_knockbackSpeed);
        var knockbackDirection =
            hitData.HitOrigin.DirectionTo(
                Enemy.GlobalPosition);
        Enemy.ApplyVelocity(knockbackDirection, _knockbackSpeed);

        _activeHitAnimation = Enemy.PlayAnimation(PlayerAnimation.Hit);
    }

    public override void Exit()
    {
        // Next entry must be configured again.
        _hitData = null;

        Enemy.EnemyAnimationFinished -= OnHitAnimationFinished;
    }

    public override void PhysicsProcess(double delta)
    {
        Enemy.DampVelocity(_decelerationRate, delta);
    }

    public void Configure(HitData hitData)
    {
        _hitData = hitData;
    }

    private void OnHitAnimationFinished(StringName animationName)
    {
        if (animationName != _activeHitAnimation)
        {
            return;
        }

        // todo Enter target tracking
        StateMachine.ResumePreviousState();
    }
}
