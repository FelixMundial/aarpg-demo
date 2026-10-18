using DAARPG.enemies.slime;
using Godot;

namespace DAARPG.enemies.states;

public abstract partial class EnemyState : Node
{
    public virtual bool AllowsReentry => false;

    public virtual bool IsInterruptState => false;

    protected EnemyStateMachine StateMachine { get; private set; } = null!;

    protected Slime Enemy => StateMachine.Enemy;

    public void Initialize(EnemyStateMachine stateMachine)
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
}
