using UnityEngine;

/// <summary>
/// Plain serializable class holding all user-configurable settings.
/// Owned and persisted by <see cref="SaveManager"/>.
/// </summary>
[System.Serializable]
public class SettingsData
{
    public const int CurrentVersion = 4;

    public int SettingsVersion = CurrentVersion;

    public float MasterVolume    = 1f;
    public float MusicVolume     = 1f;
    public float UiVolume        = 1f;
    public float GameplayVolume  = 1f;
    public float BossSfxVolume   = 0.5f;
    public float SfxVolume       = 1f;
    public float MouseSensitivity = 1f;
    public bool  InvertY         = false;
    public float Fov             = 90f;
    public bool  Fullscreen      = true;
    public int   ResolutionWidth  = 1920;
    public int   ResolutionHeight = 1080;
    public bool  SkipTutorial    = false;

    public void Normalize()
    {
        if (SettingsVersion < 2)
        {
            UiVolume = Mathf.Clamp01(MasterVolume);
            GameplayVolume = Mathf.Clamp01(SfxVolume);
            SkipTutorial = false;
        }

        if (SettingsVersion < 3)
            BossSfxVolume = 1f;

        if (SettingsVersion < 4)
            BossSfxVolume = 0.5f;

        SettingsVersion = CurrentVersion;

        MasterVolume = 1f;
        MusicVolume = Mathf.Clamp01(MusicVolume);
        UiVolume = Mathf.Clamp01(UiVolume);
        GameplayVolume = Mathf.Clamp01(GameplayVolume);
        BossSfxVolume = Mathf.Clamp01(BossSfxVolume);
        SfxVolume = GameplayVolume;
        MouseSensitivity = Mathf.Max(0.01f, MouseSensitivity);
        Fov = Mathf.Clamp(Fov, 1f, 179f);
        ResolutionWidth = Mathf.Max(1, ResolutionWidth);
        ResolutionHeight = Mathf.Max(1, ResolutionHeight);
    }
}
