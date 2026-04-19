using TMPro;
using UnityEngine;

/// <summary>
/// Displays current and carried ammo counts. Hides the container for infinite-ammo weapons
/// (those with an empty <c>ammoTypeId</c>).
/// </summary>
public class AmmoWidget : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _ammoLabel;
    [SerializeField] private GameObject      _container;

    private void Awake()
    {
        if (_ammoLabel == null) Debug.LogError("[AmmoWidget] _ammoLabel is not assigned.");
        if (_container == null) Debug.LogError("[AmmoWidget] _container is not assigned.");
    }

    /// <summary>Updates the ammo display. Pass an empty <paramref name="ammoTypeId"/> to hide.</summary>
    public void SetAmmo(string ammoTypeId, int currentAmmo, int carriedAmmo)
    {
        bool isInfinite = string.IsNullOrEmpty(ammoTypeId);

        if (_container != null)
            _container.SetActive(!isInfinite);

        if (!isInfinite && _ammoLabel != null)
            _ammoLabel.text = $"{currentAmmo} | {carriedAmmo}";
    }
}
