using UnityEngine;

/// <summary>
/// Implements <see cref="IInteractable"/>. Toggles open/closed state via an <see cref="Animator"/>
/// bool parameter "IsOpen". Optionally requires a key item in the player's <see cref="PlayerInventory"/>.
/// </summary>
[RequireComponent(typeof(Animator))]
public class Door : MonoBehaviour, IInteractable
{
    private static readonly int IS_OPEN_HASH = Animator.StringToHash("IsOpen");

    [SerializeField] private string   _requiredKeyId;
    [SerializeField] private Animator _animator;

    private bool _isOpen;
    private bool _isTransitioning;

    public bool CanInteract => !_isTransitioning;

    private void Awake()
    {
        if (_animator == null)
            _animator = GetComponent<Animator>();
    }

    /// <summary>
    /// Checks key requirement, toggles door state, and raises <see cref="DoorToggledEvent"/>.
    /// Raises <see cref="DoorLockedEvent"/> when the player lacks the required key.
    /// </summary>
    public void Interact(GameObject interactor)
    {
        if (!string.IsNullOrEmpty(_requiredKeyId))
        {
            PlayerInventory inventory = interactor.GetComponentInParent<PlayerInventory>();

            if (inventory == null || !inventory.HasItem(_requiredKeyId))
            {
                EventBus<DoorLockedEvent>.Raise(new DoorLockedEvent
                {
                    RequiredKeyId = _requiredKeyId
                });
                return;
            }
        }

        _isOpen = !_isOpen;
        _animator.SetBool(IS_OPEN_HASH, _isOpen);

        EventBus<DoorToggledEvent>.Raise(new DoorToggledEvent { IsOpen = _isOpen });
    }
}
