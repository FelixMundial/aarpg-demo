using Godot;

namespace DAARPG.gui;

public partial class HealthSlot : Control
{
    public enum SpriteFrame
    {
        Empty = 0,
        Half = 1,
        Full = 2
    }

    public const int HealthIncrementPerSlot = (int)SpriteFrame.Full;
    public const int SpriteFrameCount = (int)SpriteFrame.Full + 1;

    private Sprite2D _healthSprite = null!;

    public int Value
    {
        get => _healthSprite.Frame;

        set => _healthSprite.Frame = Mathf.Clamp(
            value,
            (int)SpriteFrame.Empty,
            (int)SpriteFrame.Full
        );
    }

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        _healthSprite = GetNode<Sprite2D>("HealthSprite");
        Value = (int)SpriteFrame.Empty;
    }
}
