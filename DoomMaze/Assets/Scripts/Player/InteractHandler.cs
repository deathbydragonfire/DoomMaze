using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Player-side component that raycasts forward on the Interact input action and calls
/// <see cref="IInteractable.Interact"/> on whatever it hits. Attach to the Player GameObject.
/// Subscribes to <see cref="InputManager"/> in <c>Start</c>, unsubscribes in <c>OnDestroy</c>.
/// </summary>
public class InteractHandler : MonoBehaviour
{
    [SerializeField] private float     _interactRange  = 2.5f;
    [SerializeField] private LayerMask _interactLayers = ~0;

    private Camera _camera;

    private bool _inputBound;

    private void Start()
    {
        _camera = Camera.main;
        EventBus<GameStateChangedEvent>.Subscribe(OnGameStateChanged);
        TryBindInput();
    }

    private void OnDestroy()
    {
        EventBus<GameStateChangedEvent>.Unsubscribe(OnGameStateChanged);
        if (InputManager.Instance != null && _inputBound)
            InputManager.Instance.Controls.Player.Interact.performed -= OnInteractPerformed;
    }

    private void OnGameStateChanged(GameStateChangedEvent e)
    {
        if (e.NewState == GameState.Playing)
            TryBindInput();
    }

    private void TryBindInput()
    {
        if (_inputBound || InputManager.Instance == null) return;
        InputManager.Instance.Controls.Player.Interact.performed += OnInteractPerformed;
        _inputBound = true;
    }

    private void OnInteractPerformed(InputAction.CallbackContext context)
    {
        if (_camera == null) return;

        bool hitInteractable = false;

        if (Physics.Raycast(_camera.transform.position, _camera.transform.forward,
                            out RaycastHit hit, _interactRange, _interactLayers))
        {
            IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();

            if (interactable != null && interactable.CanInteract)
            {
                interactable.Interact(gameObject);
                hitInteractable = true;
            }
        }

        EventBus<InteractAttemptedEvent>.Raise(new InteractAttemptedEvent
        {
            HitInteractable = hitInteractable
        });
    }
}
