using System;
using DAARPG.player.enums;
using Godot;

namespace DAARPG.enemies.states;

public partial class WanderState : EnemyState
{
    private static readonly Vector2[] WanderDirections =
    [
        Vector2.Up,
        Vector2.Down,
        Vector2.Left,
        Vector2.Right
    ];

    [Export]
    public float WalkSpeed { get; private set; } = 30.0f;

    [Export]
    private EnemyState? _nextState;

    [ExportCategory("State Control")]
    [Export(PropertyHint.Range, "0,10,0.1,suffix:s")]
    private double _animationDuration = 0.7f; // In seconds
    [Export]
    private int _minAnimationCycles = 1;
    [Export]
    private int _maxAnimationCycles = 3;

    private double _stateDuration;

    public override void Enter()
    {
        _stateDuration = GD.RandRange(_minAnimationCycles, _maxAnimationCycles) * _animationDuration;

        var randomDirection = PickRandomDirection();

        Enemy.TryUpdateFacingDirection(randomDirection);
        Enemy.ApplyVelocity(WalkSpeed);
        Enemy.PlayAnimation(PlayerAnimation.Walk);
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

    public override void PhysicsProcess(double delta)
    {
        Enemy.ApplyVelocity(WalkSpeed);
    }

    private static Vector2 PickRandomDirection()
    {
        return WanderDirections[
            Random.Shared.Next(WanderDirections.Length)
        ];
    }
}
