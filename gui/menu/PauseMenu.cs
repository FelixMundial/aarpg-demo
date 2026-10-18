using Godot;

namespace DAARPG.gui.menu;

public partial class PauseMenu : CanvasLayer
{
    private Button _resumeButton = null!;
    private Button _saveButton = null!;
    private Button _loadButton = null!;
    private Button _quitButton = null!;

    [Signal]
    public delegate void GameResumeRequestedEventHandler();

    [Signal]
    public delegate void GameSaveRequestedEventHandler();

    [Signal]
    public delegate void GameLoadRequestedEventHandler();

    [Signal]
    public delegate void GameQuitRequestedEventHandler();

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        _resumeButton = GetNode<Button>("ButtonContainer/ResumeButton");
        _saveButton = GetNode<Button>("ButtonContainer/SaveButton");
        _loadButton = GetNode<Button>("ButtonContainer/LoadButton");
        _quitButton = GetNode<Button>("ButtonContainer/QuitButton");

        _resumeButton.Pressed += OnResumeButtonPressed;
        _saveButton.Pressed += OnSaveButtonPressed;
        _loadButton.Pressed += OnLoadButtonPressed;
        _quitButton.Pressed += OnQuitButtonPressed;

        HideMenu();
    }

    public void ShowMenu()
    {
        Visible = true;
    }

    public void HideMenu()
    {
        Visible = false;
    }

    private void OnResumeButtonPressed()
    {
        EmitSignal(SignalName.GameResumeRequested);
    }

    private void OnSaveButtonPressed()
    {
        EmitSignal(SignalName.GameSaveRequested);
    }

    private void OnLoadButtonPressed()
    {
        EmitSignal(SignalName.GameLoadRequested);
    }

    private void OnQuitButtonPressed()
    {
        EmitSignal(SignalName.GameQuitRequested);
    }
}
