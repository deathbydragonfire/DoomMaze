/// <summary>
/// Plain serializable class holding all user-configurable settings.
/// Owned and persisted by <see cref="SaveManager"/>.
/// </summary>
[System.Serializable]
public class SettingsData
{
    public float MasterVolume    = 1f;
    public float MusicVolume     = 1f;
    public float SfxVolume       = 1f;
    public float MouseSensitivity = 1f;
    public bool  InvertY         = false;
    public float Fov             = 90f;
    public bool  Fullscreen      = true;
    public int   ResolutionWidth  = 1920;
    public int   ResolutionHeight = 1080;
}
