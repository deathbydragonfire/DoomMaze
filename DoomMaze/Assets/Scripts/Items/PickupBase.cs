using UnityEngine;

/// <summary>
/// Abstract base for all pickup MonoBehaviours. Owns trigger detection, event raising,
/// FX hook, and self-disabling after collection. Subclasses implement <see cref="ExecutePickup"/>.
/// </summary>
[RequireComponent(typeof(Collider))]
public abstract class PickupBase : MonoBehaviour, IPickupable
{
    [SerializeField] protected AudioClip  _pickupSound; // Phase 7 hookup
    [SerializeField] protected GameObject _fxPrefab;
    [SerializeField] [Range(0f, 1f)] protected float _pickupVolume = 1f;

    protected virtual void Awake()
    {
        if (GetComponent<PickupIdleMotion>() == null)
            gameObject.AddComponent<PickupIdleMotion>();

        if (GetComponent<PickupDropMotion>() == null)
            gameObject.AddComponent<PickupDropMotion>();
    }

    /// <summary>
    /// Final implementation. Calls <see cref="ExecutePickup"/>, raises <see cref="PickupCollectedEvent"/>,
    /// plays FX (Phase 7 placeholder), and disables this GameObject.
    /// </summary>
    public void OnPickup(PlayerInventory inventory)
    {
        if (!ExecutePickup(inventory))
            return;

        EventBus<PickupCollectedEvent>.Raise(new PickupCollectedEvent
        {
            PickupId = gameObject.name
        });

        AudioManager.Instance?.PlaySfx(_pickupSound, _pickupVolume);

        gameObject.SetActive(false);
    }

    /// <summary>Subclass-specific pickup effect applied to <paramref name="inventory"/>.</summary>
    protected abstract bool ExecutePickup(PlayerInventory inventory);

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        PickupDropMotion dropMotion = GetComponent<PickupDropMotion>();
        if (dropMotion != null && !dropMotion.CanBeCollected)
            return;

        PlayerInventory inventory = other.GetComponentInParent<PlayerInventory>();

        if (inventory == null)
        {
            Debug.LogWarning($"[PickupBase] Player collider '{other.name}' has no PlayerInventory in parent. Pickup skipped.");
            return;
        }

        OnPickup(inventory);
    }
}
