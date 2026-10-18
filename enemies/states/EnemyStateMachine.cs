using System;
using System.Collections.Generic;
using DAARPG.enemies.slime;
using Godot;

namespace DAARPG.enemies.states;

public partial class EnemyStateMachine : Node
{
    [Export]
    private EnemyState? _initialState;

    private readonly Dictionary<Type, EnemyState> _statesByType = [];

    private EnemyState? _previousState;
    private EnemyState _currentState = null!;

    private bool _initialized;

    public Slime Enemy { get; private set; } = null!;

    public Node2D? Target => Enemy.Target;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Disabled;
        ProcessPhysicsPriority = -1;
    }

    public void Initialize(Slime enemy)
    {
        if (_initialized)
        {
            GD.PushError($"{nameof(EnemyStateMachine)} can only be initialized once.");
            return;
        }

        Enemy = enemy;

        RegisterStates();

        SetInitialState();

        ProcessMode = ProcessModeEnum.Inherit;
        _initialized = true;
    }

    public override void _Process(double delta)
    {
        _currentState.Process(delta);
    }

    public override void _PhysicsProcess(double delta)
    {
        _currentState.PhysicsProcess(delta);
    }

    public void ChangeState<T>() where T : EnemyState
    {
        TryChangeState(GetSpecifiedState<T>());
    }

    public void ChangeState<T>(
        Action<T> configure
    )
        where T : EnemyState
    {
        var newState = GetSpecifiedState<T>();
        TryChangeState(
            newState,
            () => configure(newState)
        );
    }

    public void ChangeState(EnemyState newState)
    {
        TryChangeState(newState);
    }

    public T GetSpecifiedState<T>() where T : EnemyState
    {
        if (_statesByType.TryGetValue(typeof(T), out var state) && state is T typedState)
        {
            return typedState;
        }

        throw new InvalidOperationException(
            $"{Name}: No registered state of type {typeof(T).Name}" +
            $" exists under {Name}.");
    }

    public void ResumePreviousState()
    {
        if (_previousState is null)
        {
            ChangeState<IdleState>();
            return;
        }

        ChangeState(_previousState);
    }

    private void TryChangeState(
        EnemyState newState,
        Action? beforeEnter = null)
    {
        if (ReferenceEquals(newState, _currentState) &&
            !newState.AllowsReentry)
        {
            return;
        }

        var oldState = _currentState;
        oldState.Exit();

        if (!oldState.IsInterruptState)
        {
            _previousState = oldState;
        }

        _currentState = newState;

        beforeEnter?.Invoke();
        _currentState.Enter();
    }

    private void RegisterStates()
    {
        foreach (var child in GetChildren())
        {
            if (child is not EnemyState playerState)
            {
                continue;
            }

            var stateType = playerState.GetType();
            if (!_statesByType.TryAdd(stateType, playerState))
            {
                GD.PushError($"{Name}: More than one state of type {stateType.Name}" +
                             $" is registered under {Name}.");
                continue;
            }

            playerState.Initialize(this);
        }
    }

    private void SetInitialState()
    {
        _currentState = FindInitialState();
        _currentState.Enter();
    }

    private EnemyState FindInitialState()
    {
        if (_initialState is null)
        {
            return GetSpecifiedState<IdleState>();
        }

        if (IsRegisteredState(_initialState))
        {
            return _initialState;
        }

        GD.PushError($"{Name}: The initial state path '{_initialState}'" +
                     $" does not resolve to a registered state under {Name}.");

        return GetSpecifiedState<IdleState>();
    }

    private bool IsRegisteredState(EnemyState initialState)
    {
        return _statesByType.TryGetValue(initialState.GetType(), out var registeredState) &&
               ReferenceEquals(initialState, registeredState);
    }
}
