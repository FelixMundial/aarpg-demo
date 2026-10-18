using System;
using System.Collections.Generic;
using Godot;

namespace DAARPG.player.states;

/*
 * PlayerStateMachine owns state lifecycle and transitions:
   - selects active state
   - forwards callbacks
 */
public partial class PlayerStateMachine : Node
{
    [Export]
    private PlayerState? _initialState;

    private readonly Dictionary<Type, PlayerState> _statesByType = [];

    private PlayerState? _previousState;
    private PlayerState _currentState = null!;

    private bool _initialized;

    /*
     * Let the machine hold the player instead.
     * Player
       └── PlayerStateMachine
           ├── IdleState
           ├── WalkState
           └── AttackState ...
     */
    public Player Player { get; private set; } = null!;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Disabled;
        /*
         * In _Process(), Player is the producer and the state is the consumer.
         * In _PhysicsProcess(), the state is the producer of Velocity and Player the consumer.
         */
        ProcessPhysicsPriority = -1;
    }

    /*
     * Dependency Injection:
     * Player
         |
         | gives itself to
         v
       PlayerStateMachine
     */
    public void Initialize(Player player)
    {
        if (_initialized)
        {
            GD.PushError($"{Name}: {nameof(PlayerStateMachine)}" +
                         $" can only be initialized once.");
            return;
        }

        Player = player;

        RegisterStates();

        SetInitialState();

        ProcessMode = ProcessModeEnum.Inherit;
        _initialized = true;
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta)
    {
        _currentState.Process(delta);
    }

    public override void _PhysicsProcess(double delta)
    {
        _currentState.PhysicsProcess(delta);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        _currentState.ProcessInput(@event);
    }

    public void ChangeState<T>() where T : PlayerState
    {
        TryChangeState(GetSpecifiedState<T>());
    }

    public void ChangeState<T>(
        Action<T> configure
    )
        where T : PlayerState
    {
        var newState = GetSpecifiedState<T>();
        TryChangeState(
            newState,
            () => configure(newState)
        );
    }

    public void ChangeState(PlayerState newState)
    {
        TryChangeState(newState);
    }

    public T GetSpecifiedState<T>() where T : PlayerState
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

    public void ChangeState<TTrue, TFalse>(bool condition)
        where TTrue : PlayerState
        where TFalse : PlayerState
    {
        if (condition)
        {
            ChangeState<TTrue>();
        }
        else
        {
            ChangeState<TFalse>();
        }
    }

    private void TryChangeState(
        PlayerState newState,
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
            if (child is not PlayerState playerState)
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

            // The state machine becomes the context/owner of its states.
            playerState.Initialize(this);
        }

        // Historical list-based approach; it does not populate the current type-keyed dictionary.
        // _playerStates.AddRange(GetChildren().OfType<PlayerState>());
    }

    private void SetInitialState()
    {
        _currentState = FindInitialState();
        _currentState.Enter();
    }

    /*
     * Checks that a state resolved from the configured path is the same instance registered in the dictionary.
     *	If it is, that state is returned.
     *	If the path resolves to an unregistered state,
     *		the method reports an error and falls back to the registered IdleState.
     *	If the path resolves to nothing, it falls back silently.
     */
    private PlayerState FindInitialState()
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

    private bool IsRegisteredState(PlayerState initialState)
    {
        return _statesByType.TryGetValue(initialState.GetType(), out var registeredState) &&
               ReferenceEquals(initialState, registeredState);
    }
}
