using System;
using DAARPG.combat;
using DAARPG.player.enums;
using Godot;

namespace DAARPG.enemies.states;

public partial class DeathState : EnemyState
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

        Enemy.EnemyAnimationFinished += OnDeathAnimationFinished;
        Enemy.FreezeVelocity();
        _activeDeathAnimation = Enemy.PlayAnimation(PlayerAnimation.Death);
    }

    /*
     * Not called if DeathState is terminal.
     */
    public override void Exit()
    {
        Enemy.EnemyAnimationFinished -= OnDeathAnimationFinished;
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

        Enemy.CompleteDeath();
    }
}
