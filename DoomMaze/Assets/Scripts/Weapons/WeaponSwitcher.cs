using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Owns the player's weapon loadout slots, handles hotkey input (1–5),
/// triggers swap animations, and keeps <see cref="PlayerCombat"/> in sync.
/// The melee weapon is a dedicated quick-melee accessible via F at all times and is NOT a slot.
/// Empty slots are null.
/// </summary>
public class WeaponSwitcher : MonoBehaviour
{
    [SerializeField] private PlayerCombat      _playerCombat;
    [SerializeField] private ViewmodelAnimator _viewmodelAnimator;

    [Tooltip("Always-available fist/melee weapon triggered by F. Not part of the numbered slots.")]
    [SerializeField] private MeleeWeapon _quickMeleeWeapon;

    [Tooltip("Fixed 5-element array. Assign weapon MonoBehaviours in the Inspector.")]
    [SerializeField] private WeaponBase[] _weaponSlots = new WeaponBase[5];

    /// <summary>The currently active slot index (0-based).</summary>
    public int ActiveSlot { get; private set; } = -1;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private bool _inputBound;

    private void Start()
    {
        EventBus<GameStateChangedEvent>.Subscribe(OnGameStateChanged);
        InitialiseQuickMelee();
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
        {
            TryBindInput();
            TryEquipDefault();
        }
    }

    private void InitialiseQuickMelee()
    {
        if (_quickMeleeWeapon == null) return;

        _playerCombat.SetQuickMeleeWeapon(_quickMeleeWeapon);
        _quickMeleeWeapon.gameObject.SetActive(true);
    }

    private void TryBindInput()
    {
        if (_inputBound || InputManager.Instance == null) return;

        var player = InputManager.Instance.Controls.Player;

        player.Weapon1.performed += _ => SwitchToSlot(0);
        player.Weapon2.performed += _ => SwitchToSlot(1);
        player.Weapon3.performed += _ => SwitchToSlot(2);
        player.Weapon4.performed += _ => SwitchToSlot(3);
        player.Weapon5.performed += _ => SwitchToSlot(4);

        _inputBound = true;
    }

    private void UnbindInput()
    {
        if (!_inputBound || InputManager.Instance == null) return;
        _inputBound = false;
    }

    private void TryEquipDefault()
    {
        if (ActiveSlot >= 0) return;

        for (int i = 0; i < _weaponSlots.Length; i++)
        {
            if (_weaponSlots[i] == null) continue;

            ActiveSlot = i;
            _weaponSlots[i].OnEquip();
            _playerCombat.SetActiveWeapon(i, _weaponSlots[i]);
            break;
        }

        for (int i = 0; i < _weaponSlots.Length; i++)
        {
            if (i != ActiveSlot)
                _weaponSlots[i]?.OnUnequip();
        }
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Activates all non-null weapon slots. Used by debug give-weapons command.</summary>
    public void UnlockAllWeapons()
    {
        for (int i = 0; i < _weaponSlots.Length; i++)
        {
            if (_weaponSlots[i] != null)
                _weaponSlots[i].OnEquip();
        }

        if (ActiveSlot >= 0 && ActiveSlot < _weaponSlots.Length && _weaponSlots[ActiveSlot] != null)
        {
            EventBus<WeaponSwitchedEvent>.Raise(new WeaponSwitchedEvent
            {
                FromSlot  = ActiveSlot,
                ToSlot    = ActiveSlot,
                NewWeapon = _weaponSlots[ActiveSlot].Data
            });
        }
    }

    /// <summary>
    /// Switches to the weapon at <paramref name="slotIndex"/>.
    /// No-op if already active or the slot is empty.
    /// </summary>
    public void SwitchToSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= _weaponSlots.Length) return;
        if (slotIndex == ActiveSlot) return;
        if (_weaponSlots[slotIndex] == null) return;

        int fromSlot = ActiveSlot;

        if (ActiveSlot >= 0 && ActiveSlot < _weaponSlots.Length)
            _weaponSlots[ActiveSlot]?.OnUnequip();

        ActiveSlot = slotIndex;
        _weaponSlots[ActiveSlot].OnEquip();

        _viewmodelAnimator?.PlaySwap();
        _playerCombat.SetActiveWeapon(ActiveSlot, _weaponSlots[ActiveSlot]);

        EventBus<WeaponSwitchedEvent>.Raise(new WeaponSwitchedEvent
        {
            FromSlot  = fromSlot,
            ToSlot    = ActiveSlot,
            NewWeapon = _weaponSlots[ActiveSlot].Data
        });
    }
}
