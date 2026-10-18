using System;
using DAARPG.combat;
using DAARPG.combat.hitbox;
using DAARPG.combat.hurtbox;
using DAARPG.core.enums;
using DAARPG.core.extensions;
using DAARPG.player.camera;
using DAARPG.player.enums;
using DAARPG.player.states;
using Godot;
using HitState = DAARPG.player.states.HitState;

namespace DAARPG.player;

/*
 * Player owns the character and its components:
   - read input
   - owns CharacterBody2D physics
   - calls MoveAndSlide()
 */
public partial class Player : CharacterBody2D
{
    private const float MaxSupportedHealth = 100f;

    [Export]
    public CardinalDirection FacingDirection { get; private set; } = CardinalDirection.Down;

    [Export(PropertyHint.Range, "0,0.5,0.01")]
    private float _facingDirectionBias = 0.1f;

    [Export]
    public float Damage { get; private set; } = 1.0f;

    [Export]
    public float MaxHealth { get; private set; } = 6.0f;

    [Export]
    private double _defaultInvulnerableDuration = 0.5f;

    private HitBox _hitBox = null!;
    private HurtBox _hurtBox = null!;

    private Node2D _visualRoot = null!;
    private Node2D _interactionRoot = null!;

    private Timer _invulnerabilityTimer = null!;

    private AnimationPlayer _animationPlayer = null!;
    private AudioStreamPlayer2D _swordSfx = null!;
    private AnimationPlayer _fxAnimationPlayer = null!;

    private PlayerStateMachine _playerStateMachine = null!;

    private bool _isInvulnerabilityTimerConnected;
    private bool _isAnimationPlayerFinishedConnected;

    public Vector2 InputDirection { get; private set; } = Vector2.Zero;

    public float Health { get; private set; }

    public LifeState CurrentLifeState { get; set; } = LifeState.Alive;

    public bool IsInvulnerable { get; private set; }

    public event Action<StringName>? PlayerAnimationFinished;

    public event Action<float>? HealthChanged;

    public event Action<float>? MaxHealthChanged;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        // 1. Resolve mandatory components.
        _visualRoot = GetNode<Node2D>("VisualRoot");
        _interactionRoot = GetNode<Node2D>("InteractionRoot");
        _hitBox = GetNode<HitBox>("InteractionRoot/HitBox");
        _hurtBox = GetNode<HurtBox>("InteractionRoot/HurtBox");
        _invulnerabilityTimer = GetNode<Timer>("InvulnerabilityTimer");

        _animationPlayer = GetNode<AnimationPlayer>("AnimationPlayer");
        _fxAnimationPlayer = GetNode<AnimationPlayer>("FxAnimationPlayer");
        _playerStateMachine = GetNode<PlayerStateMachine>("PlayerStateMachine");

        // 2. Initialize/configure Player-owned components.
        _hitBox.Initialize(this, Damage);
        _hitBox.SetEnabled(false);

        _hurtBox.Initialize(this);
        _hurtBox.Hit += HandleHit;

        Health = MaxHealth;

        _invulnerabilityTimer.OneShot = true;
        _invulnerabilityTimer.Timeout += EndInvulnerability;
        _isInvulnerabilityTimerConnected = true;

        // 3. Permanent owner/component signals are wired once by the owner.
        _animationPlayer.AnimationFinished += OnAnimationPlayerFinished;
        _isAnimationPlayerFinishedConnected = true;

        ApplyFacingTransforms();

        // The Player owns the state machine, so it is natural for Player to initialize it.
        // 4. Initialize behavioral systems last.
        //	Important: initialize states only AFTER Player's own components/events are ready.
        _playerStateMachine.Initialize(this);
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta)
    {
        InputDirection = Input.GetVector(
            "left",
            "right",
            "up",
            "down");

        // _playerStateMachine._Process(delta);
    }

    public override void _PhysicsProcess(double delta)
    {
        // _playerStateMachine._PhysicsProcess(delta);

        MoveAndSlide();
    }

    public override void _ExitTree()
    {
        if (_hurtBox is { } hurtBox)
        {
            hurtBox.Hit -= HandleHit;
        }

        if (_invulnerabilityTimer is { } invulnerabilityTimer &&
            _isInvulnerabilityTimerConnected)
        {
            invulnerabilityTimer.Timeout -= EndInvulnerability;
            _isInvulnerabilityTimerConnected = false;
        }

        if (_animationPlayer is { } animationPlayer &&
            _isAnimationPlayerFinishedConnected)
        {
            animationPlayer.AnimationFinished -= OnAnimationPlayerFinished;
            _isAnimationPlayerFinishedConnected = false;
        }
    }

    /*
     * Initially called by GameRoot in player instantiation.
     *  Only store the value; child-node access is not yet possible here when being called.
     */
    public void ConfigureInitialFacingDirection(CardinalDirection initialDirection)
    {
        FacingDirection = initialDirection;
    }

    public void SetFacingDirection(CardinalDirection newDirection)
    {
        ConfigureInitialFacingDirection(newDirection);
        ApplyFacingTransforms();
    }

    public bool TryUpdateFacingDirection()
    {
        if (InputDirection == Vector2.Zero)
        {
            return false;
        }

        var newDirection = InputDirection.ResolveCardinalDirectionForInput(
            FacingDirection,
            _facingDirectionBias);

        if (newDirection == FacingDirection)
        {
            return false;
        }

        FacingDirection = newDirection;

        ApplyFacingTransforms();
        return true;
    }

    /*
     * States express animation intent; Player/animation controller owns the implementation.
     */
    public StringName PlayAnimation(PlayerAnimation playerAnimation)
    {
        var animationName = ResolveAnimationName(playerAnimation);

        _animationPlayer.Play(animationName);
        return animationName;
    }

    public void FreezeVelocity()
    {
        Velocity = Vector2.Zero;
    }

    public void ApplyVelocity(float speed)
    {
        ApplyVelocity(InputDirection, speed);
    }

    public void ApplyVelocity(Vector2 direction, float speed)
    {
        Velocity = direction * speed;
    }

    public void DampVelocity(float decelerationRate, double delta)
    {
        // constant linear deceleration toward zero
        // Player.Velocity = Player.Velocity.MoveToward(
        // Vector2.Zero,
        // _decelerationRate * (float)delta);

        // proportional damping — the faster you're moving, the more velocity you lose
        Velocity *= Mathf.Exp(-decelerationRate * (float)delta);
    }

    /*
     * Called by AnimationPlayer
     */
    public void OpenAttackWindow()
    {
        _hitBox.SetEnabled(true);
    }

    /*
     * Also called by AnimationPlayer
     */
    public void CloseAttackWindow()
    {
        _hitBox.SetEnabled(false);
    }

    /*
     * Called by AnimationPlayer
     */
    public void TriggerAttackSfx()
    {
        _swordSfx = GetNode<AudioStreamPlayer2D>("Audio/SwordSfx");
        _swordSfx.PitchScale = (float)GD.RandRange(0.9, 1.1);
        _swordSfx.Play();
    }

    public void TriggerFxAnimation(FxAnimation fxAnimation)
    {
        _fxAnimationPlayer.Play(fxAnimation.ToString().ToLowerInvariant());
    }

    public void StopFxAnimations()
    {
        _fxAnimationPlayer.Stop();
    }

    public void ChangeHealth(float healthChange)
    {
        SetHealth(Health + healthChange);
    }

    public void SetHealth(float health)
    {
        Health = Mathf.Clamp(health, 0f, MaxHealth);
        HealthChanged?.Invoke(Health);
    }

    public void TryUpdateMaxHealth(float newMaxHealth)
    {
        newMaxHealth = Mathf.Clamp(newMaxHealth, 1f, MaxSupportedHealth);
        if (Mathf.IsEqualApprox(MaxHealth, newMaxHealth))
        {
            return;
        }

        MaxHealth = newMaxHealth;

        var clampedHealth = Mathf.Min(Health, MaxHealth);
        var isHealthClamped = !Mathf.IsEqualApprox(clampedHealth, Health);
        Health = clampedHealth;

        MaxHealthChanged?.Invoke(MaxHealth);
        if (isHealthClamped)
        {
            HealthChanged?.Invoke(Health);
        }
    }

    public void CompleteDeath()
    {
        Respawn();
    }

    public void StartInvulnerability()
    {
        StartInvulnerability(_defaultInvulnerableDuration);
    }

    public void StartInvulnerability(double duration)
    {
        IsInvulnerable = true;
        _hurtBox.SetEnabled(false);

        // Starting an already-running timer restarts it.
        _invulnerabilityTimer.Start(duration);
    }

    public void EndInvulnerability()
    {
        if (CurrentLifeState == LifeState.Dead)
        {
            return;
        }

        _invulnerabilityTimer.Stop();
        IsInvulnerable = false;
        _hurtBox.SetEnabled(true);
    }

    public void Respawn()
    {
        GD.Print("Respawning...");
        _playerStateMachine.ChangeState<IdleState>();

        CurrentLifeState = LifeState.Alive;
        ChangeHealth(MaxHealth);
        EndInvulnerability();
    }

    /*
     * Reset transient combat/FSM state.
     */
    public void ResetState()
    {
        CurrentLifeState = LifeState.Alive;
        FreezeVelocity();

        CloseAttackWindow();
        EndInvulnerability();

        StopFxAnimations();

        _playerStateMachine.ChangeState<IdleState>();
        PlayAnimation(PlayerAnimation.Idle);
    }

    public void SetLevelBounds(Rect2I levelBounds)
    {
        GetNode<PlayerCamera>("PlayerCamera")
            .SetLevelBounds(levelBounds);
    }

    private void ApplyFacingTransforms()
    {
        var spriteScaleX = FacingDirection == CardinalDirection.Left ? -1 : 1;
        UpdateVisualTransform(spriteScaleX);
        UpdateInteractionTransform(spriteScaleX);
    }

    private void UpdateVisualTransform(int scaleX)
    {
        // 推荐操作 Sprite2D 的 Scale 属性而非将其简单flip，能顺带翻转其子节点
        _visualRoot.Scale = new Vector2(scaleX, _visualRoot.Scale.Y);
    }

    private void UpdateInteractionTransform(int scaleX)
    {
        /*
         * todo Hitbox 动态方向变化
         *	旋转父节点将导致 Hitbox 无法简单与武器Fx对齐，
         *	故改为通过 AnimationPlayer 动态设置 Hitbox Position 属性 及 父节点 Scale 属性实现
         */
        // _interactionRoot.Rotation = FacingDirection switch
        // {
        // 	CardinalDirection.Up => Mathf.Pi,
        // 	CardinalDirection.Left => Mathf.Pi / 2,
        // 	CardinalDirection.Right => -Mathf.Pi / 2,
        // 	_ => 0f
        // };
        _interactionRoot.Scale = new Vector2(scaleX, _interactionRoot.Scale.Y);
    }

    private string ResolveAnimationName(PlayerAnimation playerAnimation)
    {
        var animationName =
            $"{playerAnimation.ToString().ToLowerInvariant()}_{GetFacingDirection().ToString().ToLowerInvariant()}";
        return animationName;
    }

    private AnimationFacing GetFacingDirection()
    {
        return FacingDirection switch
        {
            CardinalDirection.Left or CardinalDirection.Right => AnimationFacing.Side,
            CardinalDirection.Up => AnimationFacing.Up,
            _ => AnimationFacing.Down
        };
    }

    private void BeginDeath(HitData hitData)
    {
        Health = 0;
        CurrentLifeState = LifeState.Dead;

        _invulnerabilityTimer.Stop();
        IsInvulnerable = false;
        _hurtBox.SetEnabled(false);

        GD.Print($"{Name} was destroyed by {hitData.SourceName}.");
        _playerStateMachine.ChangeState<DeathState>(state => state.Configure(hitData));
    }

    private void OnAnimationPlayerFinished(StringName animationName)
    {
        // Use C# event when low-level details of the animation implementation is concerned.
        PlayerAnimationFinished?.Invoke(animationName);
    }

    private void HandleHit(HitData hitData)
    {
        if (IsInvulnerable || CurrentLifeState == LifeState.Dead)
        {
            return;
        }

        ChangeHealth(-hitData.Damage);

        if (Health <= 0)
        {
            BeginDeath(hitData);
            return;
        }

        GD.Print($"{Name} was hit by {hitData.SourceName} for {hitData.Damage} damage.");
        _playerStateMachine.ChangeState<HitState>(state => state.Configure(hitData));
    }
}
