using System.Collections.Generic;
using DAARPG.player;
using Godot;

namespace DAARPG.gui;

public partial class PlayerHud : CanvasLayer
{
    private readonly List<HealthSlot> _healthSlots = [];

    [Export]
    private PackedScene _healthSlotScene = null!;

    private HFlowContainer _healthBar = null!;

    private Player? _player;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        _healthBar = GetNode<HFlowContainer>("Container/HealthBar");
        Visible = false;
    }

    public override void _ExitTree()
    {
        Unbind();
    }

    public void Bind(Player player)
    {
        Unbind();

        _player = player;
        _player.HealthChanged += HandleHealthChanged;
        _player.MaxHealthChanged += HandleMaxHealthChanged;

        SyncHealthSlots(_player.MaxHealth);
        RenderHealthSlots(_player.Health);
        Visible = true;
    }

    private void Unbind()
    {
        if (_player is null)
        {
            return;
        }

        if (IsInstanceValid(_player))
        {
            _player.HealthChanged -= HandleHealthChanged;
            _player.MaxHealthChanged -= HandleMaxHealthChanged;
        }

        _player = null;
        Visible = false;
    }

    private void SyncHealthSlots(float newMaxHealth)
    {
        var requiredMaxHealthSlots = CalculateHealthSlots(newMaxHealth);
        while (requiredMaxHealthSlots > _healthSlots.Count)
        {
            var slotToAdd = _healthSlotScene.Instantiate<HealthSlot>();
            _healthBar.AddChild(slotToAdd);
            _healthSlots.Add(slotToAdd);
        }

        while (requiredMaxHealthSlots < _healthSlots.Count)
        {
            var lastIndex = _healthSlots.Count - 1;

            var slotToRemove = _healthSlots[lastIndex];
            _healthSlots.RemoveAt(lastIndex);
            slotToRemove.QueueFree();
        }
    }

    private void RenderHealthSlots(float newHealth)
    {
        var visibleHealthSlotCount = CalculateHealthSlots(newHealth);
        for (var i = 0; i < _healthSlots.Count; i++)
        {
            var healthSlot = _healthSlots[i];

            var visible = i < visibleHealthSlotCount;
            // healthSlot.Visible = visible;
            healthSlot.Value = visible
                ? Mathf.CeilToInt(
                    Mathf.Clamp(newHealth - i, 0, 1)
                    * HealthSlot.HealthIncrementPerSlot)
                : 0;
        }
    }

    private void HandleHealthChanged(float health)
    {
        RenderHealthSlots(health);
    }

    private void HandleMaxHealthChanged(float maxHealth)
    {
        SyncHealthSlots(maxHealth);
    }

    private static int CalculateHealthSlots(float health)
    {
        return Mathf.CeilToInt(health);
    }
}
