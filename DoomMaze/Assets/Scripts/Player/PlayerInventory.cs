using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Owns the player's item list and per-type ammo counts.
/// Raises <see cref="InventoryChangedEvent"/> on every mutation.
/// </summary>
public class PlayerInventory : MonoBehaviour
{
    private readonly Dictionary<string, int> _ammoCounts = new Dictionary<string, int>(8);
    private readonly List<string>            _items      = new List<string>(16);

    // ── Ammo ─────────────────────────────────────────────────────────────────

    /// <summary>Returns the current ammo count for <paramref name="ammoType"/>.</summary>
    public int GetAmmo(string ammoType)
    {
        return _ammoCounts.TryGetValue(ammoType, out int count) ? count : 0;
    }

    /// <summary>Adds <paramref name="amount"/> ammo of <paramref name="ammoType"/>.</summary>
    public void AddAmmo(string ammoType, int amount)
    {
        if (!_ammoCounts.ContainsKey(ammoType))
            _ammoCounts[ammoType] = 0;

        _ammoCounts[ammoType] += amount;
        RaiseInventoryChanged();
    }

    /// <summary>
    /// Attempts to spend <paramref name="amount"/> ammo of <paramref name="ammoType"/>.
    /// Returns true if successful; false if insufficient ammo.
    /// </summary>
    public bool SpendAmmo(string ammoType, int amount)
    {
        if (GetAmmo(ammoType) < amount)
            return false;

        _ammoCounts[ammoType] -= amount;
        RaiseInventoryChanged();
        return true;
    }

    // ── Items ─────────────────────────────────────────────────────────────────

    /// <summary>Returns true if <paramref name="itemId"/> is in the inventory.</summary>
    public bool HasItem(string itemId)
    {
        return _items.Contains(itemId);
    }

    /// <summary>Adds <paramref name="itemId"/> to the inventory.</summary>
    public void AddItem(string itemId)
    {
        _items.Add(itemId);
        RaiseInventoryChanged();
    }

    /// <summary>
    /// Removes the first occurrence of <paramref name="itemId"/> from the inventory.
    /// Returns true if found and removed.
    /// </summary>
    public bool RemoveItem(string itemId)
    {
        bool removed = _items.Remove(itemId);
        if (removed)
            RaiseInventoryChanged();
        return removed;
    }

    // ─────────────────────────────────────────────────────────────────────────

    private void RaiseInventoryChanged()
    {
        EventBus<InventoryChangedEvent>.Raise(new InventoryChangedEvent());
    }
}
