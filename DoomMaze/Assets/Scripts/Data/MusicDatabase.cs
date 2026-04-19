using UnityEngine;

[System.Serializable]
public class MusicTrackEntry
{
    public string    TrackId;
    public AudioClip Clip;
}

/// <summary>
/// ScriptableObject that maps string track IDs to <see cref="AudioClip"/> references.
/// Single lookup point for all music. Asset naming convention: MusicDatabase.
/// </summary>
[CreateAssetMenu(menuName = "DoomMaze/Music Database", fileName = "MusicDatabase")]
public class MusicDatabase : ScriptableObject
{
    public MusicTrackEntry[] Tracks;

    /// <summary>Returns the clip for the given trackId, or null if not found.</summary>
    public AudioClip GetClip(string trackId)
    {
        if (Tracks == null) return null;

        for (int i = 0; i < Tracks.Length; i++)
        {
            if (Tracks[i].TrackId == trackId)
                return Tracks[i].Clip;
        }

        Debug.LogWarning($"[MusicDatabase] No track found with ID '{trackId}'.");
        return null;
    }
}
