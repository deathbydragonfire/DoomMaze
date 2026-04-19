#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using UnityEngine;

/// <summary>
/// Debug command that toggles infinite health by subscribing a heal-back delegate
/// to the player's <see cref="HealthComponent.OnDamaged"/> event.
/// Usage: <c>god</c>
/// </summary>
public class GodCommand : IDebugCommand
{
    public string Id          => "god";
    public string Description => "Toggles god mode (infinite health).";

    private HealthComponent          _healthComponent;
    private bool                     _godModeActive;
    private readonly Action<DamageInfo> _healDelegate;

    public GodCommand()
    {
        _healDelegate = OnDamaged;
    }

    public void Execute(string[] args, DebugConsole console)
    {
        if (_healthComponent == null)
        {
            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj == null)
            {
                console.Print("[GodCommand] Player not found in scene.");
                return;
            }
            _healthComponent = playerObj.GetComponent<HealthComponent>();
            if (_healthComponent == null)
            {
                console.Print("[GodCommand] HealthComponent not found on Player.");
                return;
            }
        }

        _godModeActive = !_godModeActive;

        if (_godModeActive)
            _healthComponent.OnDamaged += _healDelegate;
        else
            _healthComponent.OnDamaged -= _healDelegate;

        console.Print($"God mode {(_godModeActive ? "ON" : "OFF")}");
    }

    private void OnDamaged(DamageInfo info)
    {
        _healthComponent?.Heal(_healthComponent.MaxHealth);
    }
}
#endif
