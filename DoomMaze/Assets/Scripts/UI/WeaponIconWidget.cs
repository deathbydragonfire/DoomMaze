using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays the active weapon's name and its first viewmodel sprite as a HUD icon.
/// </summary>
public class WeaponIconWidget : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _weaponNameLabel;
    [SerializeField] private Image           _weaponIcon;

    private void Awake()
    {
        if (_weaponNameLabel == null) Debug.LogError("[WeaponIconWidget] _weaponNameLabel is not assigned.");
    }

    /// <summary>Updates the weapon name label and icon sprite from the given <see cref="WeaponData"/>.</summary>
    public void SetWeapon(WeaponData data)
    {
        if (data == null) return;

        if (_weaponNameLabel != null)
            _weaponNameLabel.text = data.DisplayName;

        if (_weaponIcon != null)
        {
            bool hasSprite = data.ViewmodelSprites != null && data.ViewmodelSprites.Length > 0;
            _weaponIcon.sprite  = hasSprite ? data.ViewmodelSprites[0] : null;
            _weaponIcon.enabled = hasSprite;
        }
    }
}
