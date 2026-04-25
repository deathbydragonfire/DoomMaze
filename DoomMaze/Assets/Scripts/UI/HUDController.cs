using UnityEngine;

/// <summary>
/// Root MonoBehaviour on the HUD Canvas. Subscribes to all relevant EventBus events
/// and routes data to the correct widget. Visibility is driven by <see cref="GameStateChangedEvent"/>
/// unless a scene-local override is applied.
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
    [SerializeField] private DecayMeterWidget  _decayMeterWidget;

    private Canvas          _canvas;
    private PlayerCombat    _playerCombat;
    private PlayerInventory _playerInventory;
    private PlayerDecayComponent _playerDecay;
    private bool            _isGameStateVisible = true;
    private bool?           _localVisibilityOverride;

    private void Awake()
    {
        _canvas = GetComponent<Canvas>();
        _isGameStateVisible = GameManager.Instance == null || GameManager.Instance.CurrentState == GameState.Playing;

        if (_healthWidget     == null) Debug.LogError("[HUDController] _healthWidget is not assigned.");
        if (_armorWidget      == null) Debug.LogError("[HUDController] _armorWidget is not assigned.");
        if (_ammoWidget       == null) Debug.LogError("[HUDController] _ammoWidget is not assigned.");
        if (_weaponIconWidget == null) Debug.LogError("[HUDController] _weaponIconWidget is not assigned.");
        if (_crosshairWidget  == null) Debug.LogError("[HUDController] _crosshairWidget is not assigned.");
        if (_pickupFeedWidget == null) Debug.LogError("[HUDController] _pickupFeedWidget is not assigned.");
        if (_damageFlashWidget == null) Debug.LogError("[HUDController] _damageFlashWidget is not assigned.");
        if (_superMeterWidget == null) Debug.LogError("[HUDController] _superMeterWidget is not assigned.");

        EnsureDecayMeterWidget();
        RefreshCanvasVisibility();
    }

    private void OnEnable()
    {
        EventBus<PlayerDamagedEvent>.Subscribe(OnPlayerDamaged);
        EventBus<PlayerHealedEvent>.Subscribe(OnPlayerHealed);
        EventBus<ArmorChangedEvent>.Subscribe(OnArmorChanged);
        EventBus<AmmoChangedEvent>.Subscribe(OnAmmoChanged);
        EventBus<WeaponSwitchedEvent>.Subscribe(OnWeaponSwitched);
        EventBus<InventoryChangedEvent>.Subscribe(OnInventoryChanged);
        EventBus<PickupCollectedEvent>.Subscribe(OnPickupCollected);
        EventBus<PlayerLowHealthEvent>.Subscribe(OnLowHealth);
        EventBus<GameStateChangedEvent>.Subscribe(OnGameStateChanged);
        EventBus<SuperMeterChangedEvent>.Subscribe(OnSuperMeterChanged);
        EventBus<PlayerDecayChangedEvent>.Subscribe(OnPlayerDecayChanged);
    }

    private void OnDisable()
    {
        EventBus<PlayerDamagedEvent>.Unsubscribe(OnPlayerDamaged);
        EventBus<PlayerHealedEvent>.Unsubscribe(OnPlayerHealed);
        EventBus<ArmorChangedEvent>.Unsubscribe(OnArmorChanged);
        EventBus<AmmoChangedEvent>.Unsubscribe(OnAmmoChanged);
        EventBus<WeaponSwitchedEvent>.Unsubscribe(OnWeaponSwitched);
        EventBus<InventoryChangedEvent>.Unsubscribe(OnInventoryChanged);
        EventBus<PickupCollectedEvent>.Unsubscribe(OnPickupCollected);
        EventBus<PlayerLowHealthEvent>.Unsubscribe(OnLowHealth);
        EventBus<GameStateChangedEvent>.Unsubscribe(OnGameStateChanged);
        EventBus<SuperMeterChangedEvent>.Unsubscribe(OnSuperMeterChanged);
        EventBus<PlayerDecayChangedEvent>.Unsubscribe(OnPlayerDecayChanged);
    }

    private void Start()
    {
        RefreshSuperMeter();
        RefreshDecayMeter();
    }

    public void SetLocalVisibilityOverride(bool isVisible)
    {
        _localVisibilityOverride = isVisible;
        RefreshCanvasVisibility();
    }

    public void ClearLocalVisibilityOverride()
    {
        _localVisibilityOverride = null;
        RefreshCanvasVisibility();
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
        RefreshActiveWeaponAmmo();
    }

    /// <summary>Refreshes the active weapon ammo display when carried ammo changes.</summary>
    public void OnInventoryChanged(InventoryChangedEvent e)
    {
        RefreshActiveWeaponAmmo();
    }

    public void OnPickupCollected(PickupCollectedEvent e)
    {
        _damageFlashWidget?.FlashPickup();
    }

    /// <summary>Toggles the low-health pulse warning on the health widget.</summary>
    public void OnLowHealth(PlayerLowHealthEvent e)
    {
        _healthWidget?.SetLowHealthWarning(e.IsLow);
    }

    public void OnSuperMeterChanged(SuperMeterChangedEvent e)
    {
        _superMeterWidget?.SetValue(e.ChargeNormalized, e.CurrentCharges, e.ChargesRequired, e.IsReady);
    }

    public void OnPlayerDecayChanged(PlayerDecayChangedEvent e)
    {
        _decayMeterWidget?.SetValue(e.DecayNormalized);
    }

    /// <summary>Shows or hides the entire HUD based on the current game state.</summary>
    public void OnGameStateChanged(GameStateChangedEvent e)
    {
        _isGameStateVisible = e.NewState == GameState.Playing;
        RefreshCanvasVisibility();
    }

    private void ResolvePlayerReferences()
    {
        if (_playerCombat != null && _playerInventory != null && _playerDecay != null)
            return;

        GameObject player = GameObject.FindWithTag("Player");
        if (player == null)
            return;

        _playerCombat    = player.GetComponent<PlayerCombat>();
        _playerInventory = player.GetComponent<PlayerInventory>();
        _playerDecay     = player.GetComponentInParent<PlayerDecayComponent>();
    }

    private void RefreshActiveWeaponAmmo()
    {
        ResolvePlayerReferences();

        if (_playerCombat?.ActiveWeapon?.Data == null || _playerInventory == null)
            return;

        WeaponData weaponData = _playerCombat.ActiveWeapon.Data;
        string ammoTypeId     = weaponData.AmmoTypeId;

        _ammoWidget?.SetAmmo(
            ammoTypeId,
            _playerCombat.ActiveWeapon.CurrentAmmo,
            string.IsNullOrEmpty(ammoTypeId) ? 999 : _playerInventory.GetAmmo(ammoTypeId));
    }

    private void RefreshSuperMeter()
    {
        ResolvePlayerReferences();

        if (_playerCombat == null)
            return;

        _superMeterWidget?.SetValue(
            _playerCombat.SuperChargeNormalized,
            _playerCombat.SuperKillCharge,
            _playerCombat.SuperKillsRequired,
            _playerCombat.IsSuperReady);
    }

    private void RefreshDecayMeter()
    {
        EnsureDecayMeterWidget();
        ResolvePlayerReferences();

        if (_playerDecay != null)
            _decayMeterWidget?.SetValue(_playerDecay.DecayNormalized);
        else
            _decayMeterWidget?.SetValue(1f);
    }

    private void EnsureDecayMeterWidget()
    {
        if (_decayMeterWidget != null)
            return;

        RectTransform healthRect = _healthWidget != null ? _healthWidget.transform as RectTransform : null;
        Transform parent = healthRect != null ? healthRect.parent : transform;

        GameObject meterObject = new GameObject("DecayMeterWidget", typeof(RectTransform), typeof(DecayMeterWidget));
        meterObject.transform.SetParent(parent, false);

        _decayMeterWidget = meterObject.GetComponent<DecayMeterWidget>();
        _decayMeterWidget.ConfigureRuntimeLayout(healthRect);
        _decayMeterWidget.SetValue(1f);
    }

    private void RefreshCanvasVisibility()
    {
        if (_canvas == null)
            return;

        _canvas.enabled = _localVisibilityOverride ?? _isGameStateVisible;
    }
}
