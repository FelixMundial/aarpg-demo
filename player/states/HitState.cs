#region

using System;
using DAARPG.combat;
using DAARPG.player.enums;
using Godot;

#endregion

namespace DAARPG.player.states;

public partial class HitState : PlayerState
{
    [Export(PropertyHint.Range, "1,20,0.5")]
    private float _decelerationRate = 5.0f;

    [Export(PropertyHint.Range, "0,200,10")]
    private float _knockbackSpeed = 100.0f;

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

        Player.PlayerAnimationFinished += OnHitAnimationFinished;

        var knockbackDirection =
            hitData.HitOrigin.DirectionTo(
                Player.GlobalPosition);
        Player.ApplyVelocity(knockbackDirection, _knockbackSpeed);

        Player.StartInvulnerability();
        Player.TriggerFxAnimation(FxAnimation.Hit);
        _activeHitAnimation = Player.PlayAnimation(PlayerAnimation.Hit);
    }

    public override void Exit()
    {
        // Next entry must be configured again.
        _hitData = null;

        Player.PlayerAnimationFinished -= OnHitAnimationFinished;
    }

    public override void PhysicsProcess(double delta)
    {
        Player.DampVelocity(_decelerationRate, delta);
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

        StateMachine.ResumePreviousState();
    }
}
