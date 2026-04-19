using TMPro;
using UnityEngine;

/// <summary>
/// Displays the current armor value. Hides its container when armor reaches zero.
/// </summary>
public class ArmorWidget : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _armorLabel;
    [SerializeField] private GameObject      _container;

    private void Awake()
    {
        if (_armorLabel == null) Debug.LogError("[ArmorWidget] _armorLabel is not assigned.");
        if (_container  == null) Debug.LogError("[ArmorWidget] _container is not assigned.");
    }

    /// <summary>Updates the armor label and toggles visibility based on value.</summary>
    public void SetArmor(int current)
    {
        bool hasArmor = current > 0;

        if (_container != null)
            _container.SetActive(hasArmor);

        if (_armorLabel != null)
            _armorLabel.text = current.ToString();
    }
}
