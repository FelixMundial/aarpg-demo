using DAARPG.player.enums;
using Godot;

namespace DAARPG.player.states;

/*
 * The job of WalkState:
   - play walking animation;
   - update velocity;
   - leave when movement stops.
 */
public partial class WalkState : PlayerState
{
    [Export]
    public float WalkSpeed { get; private set; } = 100.0f;

    public override void Enter()
    {
        // Start the walk animation regardless of whether facing actually changed.
        Player.TryUpdateFacingDirection();
        Player.ApplyVelocity(WalkSpeed);
        Player.PlayAnimation(PlayerAnimation.Walk);
    }

    public override void Process(double delta)
    {
        // Player owns what the facing direction is;
        //  State decides when facing is allowed to change
        //  -- While walking, movement input controls facing, which doesn't necessarily apply to other states.
        if (Player.TryUpdateFacingDirection())
        {
            Player.PlayAnimation(PlayerAnimation.Walk);
        }
    }

    public override void PhysicsProcess(double delta)
    {
        if (Player.InputDirection == Vector2.Zero)
        {
            StateMachine.ChangeState<IdleState>();
            return;
        }

        Player.ApplyVelocity(WalkSpeed);
    }

    public override void ProcessInput(InputEvent inputEvent)
    {
        if (inputEvent.IsActionPressed("attack"))
        {
            StateMachine.ChangeState<AttackState>();
        }
    }
}
