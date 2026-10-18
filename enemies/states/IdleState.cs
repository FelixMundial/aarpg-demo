using DAARPG.player.enums;
using Godot;

namespace DAARPG.enemies.states;

public partial class IdleState : EnemyState
{
    [Export]
    private EnemyState? _nextState;

    [ExportCategory("State Control")]
    [Export(PropertyHint.Range, "0,10,0.1,suffix:s")]
    private double _minStateDuration = 0.5f; // In seconds
    [Export(PropertyHint.Range, "0,10,0.1,suffix:s")]
    private double _maxStateDuration = 1.5f;

    private double _stateDuration;

    public override void Enter()
    {
        Enemy.FreezeVelocity();
        Enemy.PlayAnimation(PlayerAnimation.Idle);

        if (_nextState is null)
        {
            return;
        }

        _stateDuration = GD.RandRange(_minStateDuration, _maxStateDuration);
    }

    public override void Process(double delta)
    {
        if (_nextState is null)
        {
            return;
        }

        _stateDuration -= delta;
        if (_stateDuration <= 0.0)
        {
            StateMachine.ChangeState(_nextState);
        }
    }
}
