using System.IO;
using UnityEngine;

/// <summary>
/// Persistent singleton that owns read/write for <see cref="SaveData"/> (save.json)
/// and <see cref="SettingsData"/> (settings.json). Raises events on save/load via EventBus.
/// </summary>
public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    public SaveData     CurrentSave     { get; private set; }
    public SettingsData CurrentSettings { get; private set; }

    private const string SAVE_FILE_NAME     = "save.json";
    private const string SETTINGS_FILE_NAME = "settings.json";

    private string SaveFilePath     => Path.Combine(Application.persistentDataPath, SAVE_FILE_NAME);
    private string SettingsFilePath => Path.Combine(Application.persistentDataPath, SETTINGS_FILE_NAME);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        CurrentSave     = new SaveData();
        CurrentSettings = new SettingsData();

        LoadSettings();
    }

    // -------------------------------------------------------------------------
    // Game Save
    // -------------------------------------------------------------------------

    /// <summary>Writes <see cref="CurrentSave"/> to disk and raises <see cref="GameSavedEvent"/>.</summary>
    public void SaveGame()
    {
        try
        {
            string json = JsonUtility.ToJson(CurrentSave, prettyPrint: true);
            File.WriteAllText(SaveFilePath, json);
            EventBus<GameSavedEvent>.Raise(new GameSavedEvent());
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[SaveManager] Failed to write save file: {ex.Message}");
        }
    }

    /// <summary>
    /// Loads save from disk into <see cref="CurrentSave"/> and raises <see cref="GameLoadedEvent"/>.
    /// Returns false if no save file exists or the file is corrupt.
    /// </summary>
    public bool LoadGame()
    {
        if (!HasSaveFile())
        {
            Debug.Log("[SaveManager] No save file found.");
            return false;
        }

        try
        {
            string json = File.ReadAllText(SaveFilePath);
            CurrentSave = JsonUtility.FromJson<SaveData>(json);
            EventBus<GameLoadedEvent>.Raise(new GameLoadedEvent());
            return true;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[SaveManager] Failed to read save file: {ex.Message}");
            CurrentSave = new SaveData();
            return false;
        }
    }

    /// <summary>Deletes the save file and resets <see cref="CurrentSave"/> to defaults.</summary>
    public void DeleteSave()
    {
        try
        {
            if (File.Exists(SaveFilePath))
            {
                File.Delete(SaveFilePath);
            }

            CurrentSave = new SaveData();
            EventBus<RunResetEvent>.Raise(new RunResetEvent());
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[SaveManager] Failed to delete save file: {ex.Message}");
        }
    }

    /// <summary>Returns true if a save file exists on disk.</summary>
    public bool HasSaveFile()
    {
        return File.Exists(SaveFilePath);
    }

    // -------------------------------------------------------------------------
    // Settings
    // -------------------------------------------------------------------------

    /// <summary>Writes <see cref="CurrentSettings"/> to disk.</summary>
    public void SaveSettings()
    {
        try
        {
            if (CurrentSettings == null)
                CurrentSettings = new SettingsData();

            CurrentSettings.Normalize();

            string json = JsonUtility.ToJson(CurrentSettings, prettyPrint: true);
            File.WriteAllText(SettingsFilePath, json);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[SaveManager] Failed to write settings file: {ex.Message}");
        }
    }

    /// <summary>Loads settings from disk into <see cref="CurrentSettings"/>. Uses defaults if not found.</summary>
    public void LoadSettings()
    {
        if (!File.Exists(SettingsFilePath))
        {
            CurrentSettings = new SettingsData();
            CurrentSettings.Normalize();
            return;
        }

        try
        {
            string json = File.ReadAllText(SettingsFilePath);
            CurrentSettings = JsonUtility.FromJson<SettingsData>(json);
            if (CurrentSettings == null)
                CurrentSettings = new SettingsData();

            bool shouldSaveMigratedSettings = CurrentSettings.SettingsVersion < SettingsData.CurrentVersion;
            CurrentSettings.Normalize();

            if (shouldSaveMigratedSettings)
                SaveSettings();
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[SaveManager] Failed to read settings file: {ex.Message}");
            CurrentSettings = new SettingsData();
            CurrentSettings.Normalize();
        }
    }
}
