using DAARPG.player.enums;
using Godot;

namespace DAARPG.player.states;

public partial class AttackState : PlayerState
{
    [Export(PropertyHint.Range, "1,20,0.5")]
    private float _decelerationRate = 5.0f;

    private StringName _activeAttackAnimation = null!;

    /*
     * State-specific signals are subscribed in Enter() and removed in Exit().
     */
    public override void Enter()
    {
        // States receive dependencies; they do not search the scene tree.
        // Temporary state-relevant events are wired in Enter / Exit.
        Player.PlayerAnimationFinished += OnAttackAnimationFinished;
        // Subscribe first, then start the operation whose completion we're waiting for.
        _activeAttackAnimation = Player.PlayAnimation(PlayerAnimation.Attack);
    }

    public override void Exit()
    {
        Player.CloseAttackWindow();
        Player.PlayerAnimationFinished -= OnAttackAnimationFinished;
    }

    public override void PhysicsProcess(double delta)
    {
        Player.DampVelocity(_decelerationRate, delta);
    }

    private void OnAttackAnimationFinished(StringName animationName)
    {
        if (animationName != _activeAttackAnimation)
        {
            return;
        }

        // StateMachine.ResumePreviousState();

        if (Player.InputDirection == Vector2.Zero)
        {
            StateMachine.ChangeState<IdleState>();
        }
        else
        {
            StateMachine.ChangeState<WalkState>();
        }
        // StateMachine.ChangeState<IdleState, WalkState>(Player.Direction == Vector2.Zero);
    }
}
