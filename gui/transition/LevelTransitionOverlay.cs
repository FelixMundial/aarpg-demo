using System.Threading.Tasks;
using Godot;

namespace DAARPG.gui.transition;

public partial class LevelTransitionOverlay : CanvasLayer
{
    private AnimationPlayer _animationPlayer = null!;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        _animationPlayer = GetNode<AnimationPlayer>("Control/AnimationPlayer");
    }

    public async Task FadeOut()
    {
        _animationPlayer.Play("fade_out");
        await ToSignal(
            _animationPlayer,
            AnimationMixer.SignalName.AnimationFinished
        );
    }

    public async Task FadeIn()
    {
        _animationPlayer.Play("fade_in");
        await ToSignal(
            _animationPlayer,
            AnimationMixer.SignalName.AnimationFinished
        );
    }
}
