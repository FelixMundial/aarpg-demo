#region

using System;
using DAARPG.combat;
using DAARPG.player.enums;
using Godot;

#endregion

namespace DAARPG.player.states;

public partial class DeathState : PlayerState
{
    private StringName _activeDeathAnimation = null!;

    private HitData? _fatalHitData;

    public override void Enter()
    {
        if (_fatalHitData is not { } hitData)
        {
            throw new InvalidOperationException(
                "DeathState must be configured with HitData before Enter()."
            );
        }

        Player.PlayerAnimationFinished += OnDeathAnimationFinished;
        Player.FreezeVelocity();
        // todo Death animation not ready
        _activeDeathAnimation = Player.PlayAnimation(PlayerAnimation.Hit);
    }

    public override void Exit()
    {
        Player.PlayerAnimationFinished -= OnDeathAnimationFinished;
    }

    public void Configure(HitData hitData)
    {
        _fatalHitData = hitData;
    }

    private void OnDeathAnimationFinished(StringName animationName)
    {
        if (animationName != _activeDeathAnimation)
        {
            return;
        }

        Player.CompleteDeath();
    }
}
