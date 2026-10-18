using DAARPG.player.enums;
using Godot;

namespace DAARPG.player.states;

/*
 * The job of IdleState:
   - play idle animation when entered;
   - wait for movement input;
   - clear velocity.
 */
public partial class IdleState : PlayerState
{
    public override void Enter()
    {
        Player.FreezeVelocity();
        Player.PlayAnimation(PlayerAnimation.Idle);
    }

    public override void PhysicsProcess(double delta)
    {
        if (Player.InputDirection != Vector2.Zero)
        {
            StateMachine.ChangeState<WalkState>();
            return;
        }

        Player.FreezeVelocity();
    }

    public override void ProcessInput(InputEvent inputEvent)
    {
        if (inputEvent.IsActionPressed("attack"))
        {
            StateMachine.ChangeState<AttackState>();
        }
    }
}
