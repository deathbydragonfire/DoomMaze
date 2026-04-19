using UnityEngine;

/// <summary>
/// On player enter, writes the current player state to <see cref="SaveManager.CurrentSave"/>
/// and calls <see cref="SaveManager.SaveGame"/>. Disables itself after first use.
/// </summary>
public class CheckpointTrigger : MonoBehaviour
{
    [SerializeField] private int _checkpointIndex;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        HealthComponent health = other.GetComponentInParent<HealthComponent>();
        ArmorComponent  armor  = other.GetComponentInParent<ArmorComponent>();

        if (SaveManager.Instance == null) return;

        SaveData save = SaveManager.Instance.CurrentSave;

        save.CheckpointIndex   = _checkpointIndex;
        save.CurrentHealth     = health != null ? health.CurrentHealth : save.CurrentHealth;
        save.CurrentArmor      = armor  != null ? armor.CurrentArmor   : save.CurrentArmor;

        Vector3 position = other.transform.position;
        save.CheckpointPositionX = position.x;
        save.CheckpointPositionY = position.y;
        save.CheckpointPositionZ = position.z;

        SaveManager.Instance.SaveGame();

        gameObject.SetActive(false);
    }
}
