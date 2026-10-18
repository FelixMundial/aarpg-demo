using System;
using DAARPG.gui.menu;
using Godot;

namespace DAARPG.system.controller;

public class MenuController(SceneTree sceneTree, PauseMenu pauseMenu)
{
    private readonly SceneTree _sceneTree
        = sceneTree ?? throw new ArgumentNullException(nameof(sceneTree));
    private readonly PauseMenu _pauseMenu
        = pauseMenu ?? throw new ArgumentNullException(nameof(pauseMenu));

    public bool IsGamePaused => _sceneTree.Paused;

    public void ToggleGamePause()
    {
        if (IsGamePaused)
        {
            ResumeGame();
        }
        else
        {
            PauseGame();
        }
    }

    public void PauseGame()
    {
        if (IsGamePaused)
        {
            return;
        }

        _sceneTree.Paused = true;
        _pauseMenu.ShowMenu();
    }

    public void ResumeGame()
    {
        if (!IsGamePaused)
        {
            return;
        }

        _sceneTree.Paused = false;
        _pauseMenu.HideMenu();
    }

    public void HideMenu()
    {
        _pauseMenu.HideMenu();
    }

    public void ShowMenu()
    {
        _pauseMenu.ShowMenu();
    }

    public void QuitGame()
    {
        // todo 
        // confirmation popup
        // GameSessionManager
        // ApplicationLifecycleManager
        GD.Print($"{nameof(MenuController)}: Quitting game...");
        _sceneTree.Quit();
    }
}
