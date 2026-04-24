using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Assigns upgrade choices to the pedestals already placed inside an upgrade room.
/// </summary>
public class UpgradeRoomController : MonoBehaviour
{
    [SerializeField] private UpgradeDatabase _upgradeDatabase;
    [SerializeField] private AudioClip _upgradeRoomMusic;
    [SerializeField] [Range(0f, 1f)] private float _upgradeRoomMusicVolume = 0.85f;
    [SerializeField] private float _musicFadeOutDuration = 0.45f;
    [SerializeField] private float _musicFadeInDuration = 0.45f;
    [SerializeField] private float _musicFadeOutOnExitDuration = 0.35f;
    [SerializeField] private Vector3 _musicTriggerPadding = new Vector3(2f, 4f, 2f);

    [Header("Upgrade Fog")]
    [SerializeField] private Color _upgradeFogColor = Color.black;
    [SerializeField] private Color _outsideFogColor = new Color(0.7f, 0.03f, 0.02f, 1f);
    [SerializeField] private float _fogCrossfadeDuration = 1.5f;

    private readonly List<UpgradePedestal> _pedestals = new List<UpgradePedestal>(3);
    private readonly List<Collider> _roomColliders = new List<Collider>(16);
    private Bounds _localBounds = new Bounds(Vector3.zero, new Vector3(40f, 8f, 40f));
    private Transform _playerTransform;
    private static UpgradeRoomController _upgradeFogOwner;
    private bool _configured;
    private bool _hasChosenUpgrade;
    private bool _isPlayerInRoom;
    private bool _isMusicOverrideActive;
    private UpgradeRoomMusicTrigger _musicTrigger;

    public static bool IsPlayerInAnyUpgradeRoom => _upgradeFogOwner != null;

    private void Start()
    {
        ConfigureRoom();
    }

    private void OnDisable()
    {
        if (_upgradeFogOwner == this)
        {
            _upgradeFogOwner = null;
            SetPlayerPresence(false);
            EnemyRoomController.RequestFogColor(this, _outsideFogColor, _fogCrossfadeDuration);
        }
        else if (_isPlayerInRoom)
        {
            SetPlayerPresence(false);
        }

        if (_isMusicOverrideActive)
            StopUpgradeRoomMusic();
    }

    private void Update()
    {
        if (!_configured)
            return;

        UpdateUpgradeRoomFogPresence();
    }

    private void UpdateUpgradeRoomFogPresence()
    {
        if (_playerTransform == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
                _playerTransform = player.transform;
        }

        bool playerInRoom = _playerTransform != null && IsPlayerStandingOnThisRoom(_playerTransform.position);
        if (playerInRoom)
        {
            SetUpgradeFogOwner(this);
            PlayUpgradeRoomMusic();
            return;
        }

        if (!_isPlayerInRoom && _upgradeFogOwner != this)
            return;

        SetPlayerPresence(false);
        if (_upgradeFogOwner == this)
        {
            _upgradeFogOwner = null;
            EnemyRoomController.RequestFogColor(this, _outsideFogColor, _fogCrossfadeDuration);
        }

        StopUpgradeRoomMusic();
    }

    public void Configure(UpgradeDatabase upgradeDatabase, UpgradePickup upgradePickupPrefab, Vector3[] choiceLocalOffsets)
    {
        _upgradeDatabase = upgradeDatabase;
        ConfigureRoom();
    }

    public void ConfigureMusic(AudioClip upgradeRoomMusic, float volume, float fadeOutDuration, float fadeInDuration, float fadeOutOnExitDuration)
    {
        _upgradeRoomMusic = upgradeRoomMusic;
        _upgradeRoomMusicVolume = Mathf.Clamp01(volume);
        _musicFadeOutDuration = Mathf.Max(0.01f, fadeOutDuration);
        _musicFadeInDuration = Mathf.Max(0.01f, fadeInDuration);
        _musicFadeOutOnExitDuration = Mathf.Max(0.01f, fadeOutOnExitDuration);
        EnsureMusicTrigger();
    }

    public void ChoosePedestal(UpgradePedestal pedestal)
    {
        if (_hasChosenUpgrade || pedestal == null || pedestal.UpgradeData == null)
            return;

        RunUpgradeManager manager = RunUpgradeManager.Instance;
        if (!manager.ApplyUpgrade(pedestal.UpgradeData, out int rank))
            return;

        _hasChosenUpgrade = true;
        string displayName = GetDisplayName(pedestal.UpgradeData);
        EventBus<PickupFeedMessageEvent>.Raise(new PickupFeedMessageEvent
        {
            Message = $"{displayName.ToUpperInvariant()} RANK {rank}/{Mathf.Max(1, pedestal.UpgradeData.MaxRank)}",
            Tint = new Color(0.35f, 1f, 0.42f, 1f)
        });

        for (int i = 0; i < _pedestals.Count; i++)
        {
            UpgradePedestal choice = _pedestals[i];
            if (choice == null)
                continue;

            if (choice == pedestal)
                choice.MarkChosen();
            else
                choice.MarkUnavailable();
        }
    }

    public void DisableOtherChoices(UpgradePickup collected)
    {
        // Kept for legacy UpgradePickup references; generated rooms now use UpgradePedestal.
    }

    public void HandlePlayerEnteredMusicZone()
    {
        if (_playerTransform == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
                _playerTransform = player.transform;
        }

        if (_playerTransform != null && IsPlayerStandingOnThisRoom(_playerTransform.position))
        {
            SetUpgradeFogOwner(this);
            PlayUpgradeRoomMusic();
        }
    }

    public void HandlePlayerExitedMusicZone()
    {
        UpdateUpgradeRoomFogPresence();

        if (_playerTransform == null || !IsPlayerStandingOnThisRoom(_playerTransform.position))
            StopUpgradeRoomMusic();
    }

    private void ConfigureRoom()
    {
        if (_configured)
            return;

        _configured = true;
        CachePedestals();
        CacheRoomBounds();
        CacheRoomColliders();
        AssignChoices();
        EnsureMusicTrigger();
    }

    private void SetUpgradeFogOwner(UpgradeRoomController owner)
    {
        if (owner == null)
            return;

        if (_upgradeFogOwner != null && _upgradeFogOwner != owner)
            _upgradeFogOwner.SetPlayerPresence(false);

        _upgradeFogOwner = owner;
        owner.SetPlayerPresence(true);
        EnemyRoomController.RequestFogColor(owner, owner._upgradeFogColor, owner._fogCrossfadeDuration);
    }

    private void SetPlayerPresence(bool isInRoom)
    {
        if (_isPlayerInRoom == isInRoom)
            return;

        _isPlayerInRoom = isInRoom;
        EventBus<UpgradeRoomPresenceChangedEvent>.Raise(new UpgradeRoomPresenceChangedEvent
        {
            Room = this,
            IsPlayerInside = isInRoom
        });
    }

    private void PlayUpgradeRoomMusic()
    {
        if (_isMusicOverrideActive)
            return;

        _isMusicOverrideActive = true;

        if (_upgradeRoomMusic != null)
        {
            MusicManager.Instance?.PlayTemporaryClip(
                _upgradeRoomMusic,
                _upgradeRoomMusicVolume,
                _musicFadeOutDuration,
                _musicFadeInDuration);
        }
        else
        {
            MusicManager.Instance?.PlayTemporarySilence(_musicFadeOutDuration);
        }
    }

    private void StopUpgradeRoomMusic()
    {
        if (!_isMusicOverrideActive)
            return;

        _isMusicOverrideActive = false;
        MusicManager.Instance?.StopTemporaryClip(_musicFadeOutOnExitDuration);
    }

    private void CachePedestals()
    {
        _pedestals.Clear();

        UpgradePedestal[] existingPedestals = GetComponentsInChildren<UpgradePedestal>(true);
        for (int i = 0; i < existingPedestals.Length; i++)
            AddPedestal(existingPedestals[i]);

        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child == null || !child.name.StartsWith("Upgrade Pedestal"))
                continue;

            UpgradePedestal pedestal = child.GetComponent<UpgradePedestal>();
            if (pedestal == null)
                pedestal = child.gameObject.AddComponent<UpgradePedestal>();

            AddPedestal(pedestal);
        }

        _pedestals.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));
    }

    private void AddPedestal(UpgradePedestal pedestal)
    {
        if (pedestal != null && !_pedestals.Contains(pedestal))
            _pedestals.Add(pedestal);
    }

    private void AssignChoices()
    {
        if (_pedestals.Count == 0)
            return;

        RunUpgradeManager manager = RunUpgradeManager.Instance;
        List<UpgradeData> choices = _upgradeDatabase != null
            ? _upgradeDatabase.GetRandomChoices(_pedestals.Count, manager)
            : UpgradeDatabase.GetDefaultRandomChoices(_pedestals.Count, manager);

        for (int i = 0; i < _pedestals.Count; i++)
        {
            UpgradePedestal pedestal = _pedestals[i];
            if (pedestal == null)
                continue;

            if (choices != null && i < choices.Count)
                pedestal.Configure(choices[i], this);
            else
                pedestal.MarkUnavailable();
        }
    }

    private void EnsureMusicTrigger()
    {
        if (_upgradeRoomMusic == null || _musicTrigger != null)
            return;

        GameObject triggerObject = new GameObject("UpgradeRoomMusicTrigger", typeof(BoxCollider));
        triggerObject.transform.SetParent(transform, false);

        Bounds bounds = CalculateRoomBounds(out bool hasBounds);
        Vector3 localCenter = hasBounds ? transform.InverseTransformPoint(bounds.center) : Vector3.zero;
        Vector3 size = hasBounds ? bounds.size + _musicTriggerPadding : new Vector3(48f, 16f, 48f);

        triggerObject.transform.localPosition = localCenter;
        triggerObject.transform.localRotation = Quaternion.identity;

        BoxCollider collider = triggerObject.GetComponent<BoxCollider>();
        collider.isTrigger = true;
        collider.center = Vector3.zero;
        collider.size = new Vector3(
            Mathf.Max(1f, size.x),
            Mathf.Max(1f, size.y),
            Mathf.Max(1f, size.z));

        _musicTrigger = triggerObject.AddComponent<UpgradeRoomMusicTrigger>();
        _musicTrigger.Configure(this);
    }

    private void CacheRoomBounds()
    {
        bool hasBounds = false;
        _localBounds = new Bounds(Vector3.zero, Vector3.one);

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || renderer.GetComponentInParent<UpgradePedestal>() != null)
                continue;

            EncapsulateWorldBounds(renderer.bounds, ref hasBounds);
        }

        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null || collider.isTrigger || collider.GetComponentInParent<UpgradePedestal>() != null)
                continue;

            EncapsulateWorldBounds(collider.bounds, ref hasBounds);
        }

        if (!hasBounds)
            _localBounds = new Bounds(Vector3.zero, new Vector3(40f, 8f, 40f));

        _localBounds.Expand(new Vector3(-2f, 0f, -2f));
        if (_localBounds.size.x <= 1f || _localBounds.size.z <= 1f)
            _localBounds = new Bounds(Vector3.zero, new Vector3(40f, 8f, 40f));
    }

    private void CacheRoomColliders()
    {
        _roomColliders.Clear();

        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null || collider.isTrigger || collider.GetComponentInParent<UpgradePedestal>() != null)
                continue;

            _roomColliders.Add(collider);
        }
    }

    private void EncapsulateWorldBounds(Bounds worldBounds, ref bool hasBounds)
    {
        Vector3 min = worldBounds.min;
        Vector3 max = worldBounds.max;

        EncapsulateLocalPoint(new Vector3(min.x, min.y, min.z), ref hasBounds);
        EncapsulateLocalPoint(new Vector3(min.x, min.y, max.z), ref hasBounds);
        EncapsulateLocalPoint(new Vector3(min.x, max.y, min.z), ref hasBounds);
        EncapsulateLocalPoint(new Vector3(min.x, max.y, max.z), ref hasBounds);
        EncapsulateLocalPoint(new Vector3(max.x, min.y, min.z), ref hasBounds);
        EncapsulateLocalPoint(new Vector3(max.x, min.y, max.z), ref hasBounds);
        EncapsulateLocalPoint(new Vector3(max.x, max.y, min.z), ref hasBounds);
        EncapsulateLocalPoint(new Vector3(max.x, max.y, max.z), ref hasBounds);
    }

    private void EncapsulateLocalPoint(Vector3 worldPoint, ref bool hasBounds)
    {
        Vector3 localPoint = transform.InverseTransformPoint(worldPoint);
        if (!hasBounds)
        {
            _localBounds = new Bounds(localPoint, Vector3.zero);
            hasBounds = true;
            return;
        }

        _localBounds.Encapsulate(localPoint);
    }

    private bool IsPlayerStandingOnThisRoom(Vector3 playerPosition)
    {
        if (_roomColliders.Count == 0)
            return false;

        Vector3 rayOrigin = playerPosition + Vector3.up * 3f;
        Ray ray = new Ray(rayOrigin, Vector3.down);
        for (int i = 0; i < _roomColliders.Count; i++)
        {
            Collider collider = _roomColliders[i];
            if (collider == null || collider.isTrigger)
                continue;

            if (collider.Raycast(ray, out RaycastHit hit, 8f))
            {
                Vector3 hitLocal = transform.InverseTransformPoint(hit.point);
                return hitLocal.x >= _localBounds.min.x && hitLocal.x <= _localBounds.max.x &&
                       hitLocal.z >= _localBounds.min.z && hitLocal.z <= _localBounds.max.z;
            }
        }

        return false;
    }

    private Bounds CalculateRoomBounds(out bool hasBounds)
    {
        Bounds bounds = new Bounds(transform.position, Vector3.zero);
        hasBounds = false;

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null || collider.isTrigger)
                continue;

            if (!hasBounds)
            {
                bounds = collider.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(collider.bounds);
            }
        }

        return bounds;
    }

    private static string GetDisplayName(UpgradeData upgrade)
    {
        if (upgrade == null)
            return "Upgrade";

        return !string.IsNullOrWhiteSpace(upgrade.DisplayName) ? upgrade.DisplayName : upgrade.UpgradeId;
    }
}
