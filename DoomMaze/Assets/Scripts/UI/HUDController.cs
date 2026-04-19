using UnityEngine;

/// <summary>
/// Root MonoBehaviour on the HUD Canvas. Subscribes to all relevant EventBus events
/// and routes data to the correct widget. Visibility is driven by <see cref="GameStateChangedEvent"/>.
/// </summary>
public class HUDController : MonoBehaviour
{
    [SerializeField] private HealthWidget      _healthWidget;
    [SerializeField] private ArmorWidget       _armorWidget;
    [SerializeField] private AmmoWidget        _ammoWidget;
    [SerializeField] private WeaponIconWidget  _weaponIconWidget;
    [SerializeField] private CrosshairWidget   _crosshairWidget;
    [SerializeField] private PickupFeedWidget  _pickupFeedWidget;
    [SerializeField] private DamageFlashWidget _damageFlashWidget;
    [SerializeField] private SuperMeterWidget  _superMeterWidget;

    private Canvas _canvas;

    private void Awake()
    {
        _canvas = GetComponent<Canvas>();

        if (_healthWidget     == null) Debug.LogError("[HUDController] _healthWidget is not assigned.");
        if (_armorWidget      == null) Debug.LogError("[HUDController] _armorWidget is not assigned.");
        if (_ammoWidget       == null) Debug.LogError("[HUDController] _ammoWidget is not assigned.");
        if (_weaponIconWidget == null) Debug.LogError("[HUDController] _weaponIconWidget is not assigned.");
        if (_crosshairWidget  == null) Debug.LogError("[HUDController] _crosshairWidget is not assigned.");
        if (_pickupFeedWidget == null) Debug.LogError("[HUDController] _pickupFeedWidget is not assigned.");
        if (_damageFlashWidget == null) Debug.LogError("[HUDController] _damageFlashWidget is not assigned.");
        if (_superMeterWidget == null) Debug.LogError("[HUDController] _superMeterWidget is not assigned.");
    }

    private void OnEnable()
    {
        EventBus<PlayerDamagedEvent>.Subscribe(OnPlayerDamaged);
        EventBus<PlayerHealedEvent>.Subscribe(OnPlayerHealed);
        EventBus<ArmorChangedEvent>.Subscribe(OnArmorChanged);
        EventBus<AmmoChangedEvent>.Subscribe(OnAmmoChanged);
        EventBus<WeaponSwitchedEvent>.Subscribe(OnWeaponSwitched);
        EventBus<InventoryChangedEvent>.Subscribe(OnInventoryChanged);
        EventBus<PlayerLowHealthEvent>.Subscribe(OnLowHealth);
        EventBus<GameStateChangedEvent>.Subscribe(OnGameStateChanged);
    }

    private void OnDisable()
    {
        EventBus<PlayerDamagedEvent>.Unsubscribe(OnPlayerDamaged);
        EventBus<PlayerHealedEvent>.Unsubscribe(OnPlayerHealed);
        EventBus<ArmorChangedEvent>.Unsubscribe(OnArmorChanged);
        EventBus<AmmoChangedEvent>.Unsubscribe(OnAmmoChanged);
        EventBus<WeaponSwitchedEvent>.Unsubscribe(OnWeaponSwitched);
        EventBus<InventoryChangedEvent>.Unsubscribe(OnInventoryChanged);
        EventBus<PlayerLowHealthEvent>.Unsubscribe(OnLowHealth);
        EventBus<GameStateChangedEvent>.Unsubscribe(OnGameStateChanged);
    }

    /// <summary>Updates health display and triggers damage flash.</summary>
    public void OnPlayerDamaged(PlayerDamagedEvent e)
    {
        _healthWidget?.SetHealth(e.CurrentHealth, e.MaxHealth);
        _damageFlashWidget?.Flash();
    }

    /// <summary>Updates health display on heal.</summary>
    public void OnPlayerHealed(PlayerHealedEvent e)
    {
        _healthWidget?.SetHealth(e.CurrentHealth, e.MaxHealth);
    }

    /// <summary>Updates armor display.</summary>
    public void OnArmorChanged(ArmorChangedEvent e)
    {
        _armorWidget?.SetArmor(e.CurrentArmor);
    }

    /// <summary>Updates ammo display.</summary>
    public void OnAmmoChanged(AmmoChangedEvent e)
    {
        _ammoWidget?.SetAmmo(e.AmmoTypeId, e.CurrentAmmo, e.CarriedAmmo);
    }

    /// <summary>Updates weapon icon and ammo display on weapon switch.</summary>
    public void OnWeaponSwitched(WeaponSwitchedEvent e)
    {
        _weaponIconWidget?.SetWeapon(e.NewWeapon);
    }

    /// <summary>Reserved for future inventory widget updates.</summary>
    public void OnInventoryChanged(InventoryChangedEvent e)
    {
    }

    /// <summary>Toggles the low-health pulse warning on the health widget.</summary>
    public void OnLowHealth(PlayerLowHealthEvent e)
    {
        _healthWidget?.SetLowHealthWarning(e.IsLow);
    }

    /// <summary>Shows or hides the entire HUD based on the current game state.</summary>
    public void OnGameStateChanged(GameStateChangedEvent e)
    {
        if (_canvas != null)
        {
            _canvas.enabled = e.NewState == GameState.Playing;
        }
    }
}
