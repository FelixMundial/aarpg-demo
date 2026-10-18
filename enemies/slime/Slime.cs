using System;
using DAARPG.combat;
using DAARPG.combat.hitbox;
using DAARPG.combat.hurtbox;
using DAARPG.core.enums;
using DAARPG.core.extensions;
using DAARPG.enemies.states;
using DAARPG.player.enums;
using Godot;

namespace DAARPG.enemies.slime;

public partial class Slime : CharacterBody2D
{
    [Export]
    public CardinalDirection FacingDirection { get; set; } = CardinalDirection.Down;

    [Export]
    public float Damage { get; private set; } = 0.5f;

    [Export]
    public float MaxHealth { get; private set; } = 3.0f;

    private Sprite2D _bodySprite = null!;

    private HitBox _hitBox = null!;
    private HurtBox _hurtBox = null!;

    private AnimationPlayer _animationPlayer = null!;

    private EnemyStateMachine _enemyStateMachine = null!;

    private bool _isAnimationPlayerFinishedConnected;

    public Vector2 Direction { get; private set; } = Vector2.Zero;

    public LifeState CurrentLifeState { get; private set; } = LifeState.Alive;

    public float Health { get; private set; }

    public Node2D? Target { get; private set; }

    public event Action<StringName>? EnemyAnimationFinished;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        _bodySprite = GetNode<Sprite2D>("BodySprite");
        _hitBox = GetNode<HitBox>("HitBox");
        _hurtBox = GetNode<HurtBox>("HurtBox");

        _animationPlayer = GetNode<AnimationPlayer>("AnimationPlayer");
        _enemyStateMachine = GetNode<EnemyStateMachine>("EnemyStateMachine");

        _hitBox.Initialize(this, Damage);
        _hitBox.SetEnabled(true);

        _hurtBox.Initialize(this);
        _hurtBox.Hit += HandleHit;

        Health = MaxHealth;

        _animationPlayer.AnimationFinished += OnAnimationPlayerFinished;
        _isAnimationPlayerFinishedConnected = true;

        _enemyStateMachine.Initialize(this);
    }

    public void Initialize(Node2D target)
    {
        // Inject dependencies supplied by something outside this scene, such as the targets.
        SetTarget(target);
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta)
    {
    }

    public override void _PhysicsProcess(double delta)
    {
        MoveAndSlide();
    }

    public override void _ExitTree()
    {
        if (_hurtBox is { } hurtBox)
        {
            hurtBox.Hit -= HandleHit;
        }

        if (_animationPlayer is { } animationPlayer &&
            _isAnimationPlayerFinishedConnected)
        {
            animationPlayer.AnimationFinished -= OnAnimationPlayerFinished;
            _isAnimationPlayerFinishedConnected = false;
        }
    }

    public bool TryUpdateFacingDirection(Vector2 direction)
    {
        Direction = direction;
        if (Direction == Vector2.Zero)
        {
            return false;
        }

        var newDirection = Direction.ResolveCardinalDirectionForInput(FacingDirection);

        if (newDirection == FacingDirection)
        {
            return false;
        }

        FacingDirection = newDirection;

        UpdateTransformOnDirectionChange();
        return true;
    }

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
        ApplyVelocity(Direction, speed);
    }

    public void ApplyVelocity(Vector2 direction, float speed)
    {
        Velocity = direction * speed;
    }

    public void DampVelocity(float decelerationRate, double delta)
    {
        Velocity *= Mathf.Exp(-decelerationRate * (float)delta);
    }

    public void SetTarget(Node2D? target)
    {
        Target = target;
    }

    public void CompleteDeath()
    {
        QueueFree();
    }

    private void UpdateTransformOnDirectionChange()
    {
        var spriteScaleX = FacingDirection == CardinalDirection.Left ? -1 : 1;
        UpdateVisualTransform(spriteScaleX);
    }

    private void UpdateVisualTransform(int scaleX)
    {
        _bodySprite.Scale = new Vector2(scaleX, _bodySprite.Scale.Y);
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

    private void ChangeHealth(float healthChange)
    {
        Health = Mathf.Clamp(Health + healthChange, 0, MaxHealth);
    }

    private void BeginDeath(HitData hitData)
    {
        Health = 0;
        CurrentLifeState = LifeState.Dead;
        _hurtBox.SetEnabled(false);

        GD.Print($"{Name} was destroyed by {hitData.SourceName}.");
        _enemyStateMachine.ChangeState<DeathState>(state => state.Configure(hitData));
    }

    private void OnAnimationPlayerFinished(StringName animationName)
    {
        EnemyAnimationFinished?.Invoke(animationName);
    }

    private void HandleHit(HitData hitData)
    {
        if (CurrentLifeState == LifeState.Dead)
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
        _enemyStateMachine.ChangeState<HitState>(state => state.Configure(hitData));
    }
}
