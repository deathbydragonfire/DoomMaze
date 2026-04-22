using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Bridge between player input and the active weapon.
/// Subscribes to Fire, AltFire, and Melee input actions and delegates to <see cref="IWeapon"/>.
/// The Melee action always fires the quick-melee weapon regardless of the active slot.
/// Supports both semi-auto (performed callback) and full-auto (held poll in Update).
/// </summary>
public class PlayerCombat : MonoBehaviour
{
    /// <summary>Currently active weapon slot index (0-based).</summary>
    public int ActiveWeaponSlot { get; private set; }

    private IWeapon      _activeWeapon;
    private MeleeWeapon  _quickMeleeWeapon;
    private bool         _isFiring;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private bool _inputBound;

    private void Start()
    {
        EventBus<GameStateChangedEvent>.Subscribe(OnGameStateChanged);
        TryBindInput();
    }

    private void OnDestroy()
    {
        EventBus<GameStateChangedEvent>.Unsubscribe(OnGameStateChanged);
        UnbindInput();
    }

    private void OnGameStateChanged(GameStateChangedEvent e)
    {
        if (e.NewState == GameState.Playing)
            TryBindInput();
    }

    private void TryBindInput()
    {
        if (_inputBound || InputManager.Instance == null) return;

        var player = InputManager.Instance.Controls.Player;

        player.Fire.performed    += OnFirePerformed;
        player.Fire.canceled     += OnFireCanceled;
        player.AltFire.performed += OnAltFirePerformed;
        player.Melee.performed   += OnMeleePerformed;

        _inputBound = true;
    }

    private void UnbindInput()
    {
        if (!_inputBound || InputManager.Instance == null) return;

        var player = InputManager.Instance.Controls.Player;

        player.Fire.performed    -= OnFirePerformed;
        player.Fire.canceled     -= OnFireCanceled;
        player.AltFire.performed -= OnAltFirePerformed;
        player.Melee.performed   -= OnMeleePerformed;

        _inputBound = false;
    }

    private void Update()
    {
        // Full-auto support: poll CanFire() every frame while the fire button is held.
        if (_isFiring && _activeWeapon != null && _activeWeapon.Data?.FireMode == FireMode.Auto)
            _activeWeapon.Fire();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Sets the active weapon slot and raises <see cref="WeaponEquippedEvent"/>.</summary>
    public void SetActiveWeapon(int slot)
    {
        ActiveWeaponSlot = slot;
        EventBus<WeaponEquippedEvent>.Raise(new WeaponEquippedEvent { SlotIndex = slot });
    }

    /// <summary>
    /// Sets both the active slot index and the <see cref="IWeapon"/> reference.
    /// Called by <see cref="WeaponSwitcher"/> on every slot change.
    /// </summary>
    public void SetActiveWeapon(int slot, IWeapon weapon)
    {
        ActiveWeaponSlot = slot;
        _activeWeapon    = weapon;
        EventBus<WeaponEquippedEvent>.Raise(new WeaponEquippedEvent { SlotIndex = slot });
    }

    /// <summary>
    /// Registers the always-available quick-melee weapon triggered by the Melee keybind (F).
    /// Called by <see cref="WeaponSwitcher"/> during initialisation.
    /// </summary>
    public void SetQuickMeleeWeapon(MeleeWeapon meleeWeapon)
    {
        _quickMeleeWeapon = meleeWeapon;
    }

    // ── Input handlers ────────────────────────────────────────────────────────

    private void OnFirePerformed(InputAction.CallbackContext context)
    {
        _isFiring = true;

        // Semi-auto fires on the performed callback only.
        if (_activeWeapon?.Data?.FireMode == FireMode.Semi)
            _activeWeapon.Fire();
    }

    private void OnFireCanceled(InputAction.CallbackContext context)
    {
        _isFiring = false;
    }

    private void OnAltFirePerformed(InputAction.CallbackContext context)
    {
        _activeWeapon?.AltFire();
    }

    private void OnMeleePerformed(InputAction.CallbackContext context)
    {
        _quickMeleeWeapon?.Fire();
    }
}
