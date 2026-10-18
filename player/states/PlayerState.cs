using Godot;

namespace DAARPG.player.states;

/*
 * PlayerState owns behavior specific to that state:
   - decides behavior
   - modifies velocity
   - triggers transitions
 */
public abstract partial class PlayerState : Node
{
    public virtual bool AllowsReentry => false;

    public virtual bool IsInterruptState => false;

    /*
     * Let each state know its owning state machine, and let the machine hold the player instead.
     */
    protected PlayerStateMachine StateMachine { get; private set; } = null!;

    protected Player Player => StateMachine.Player;

    /*
     * Dependency Injection:
     * PlayerStateMachine
         |
         | gives itself to
         v
       PlayerStates
     */
    public void Initialize(PlayerStateMachine stateMachine)
    {
        StateMachine = stateMachine;
    }

    public virtual void Enter()
    {
    }

    public virtual void Exit()
    {
    }

    public virtual void Process(double delta)
    {
    }

    public virtual void PhysicsProcess(double delta)
    {
    }

    public virtual void ProcessInput(InputEvent inputEvent)
    {
    }
}
