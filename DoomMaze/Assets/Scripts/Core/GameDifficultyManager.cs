public enum GameDifficulty
{
    Easy,
    Normal,
    Hard
}

public readonly struct GameDifficultyProfile
{
    public GameDifficultyProfile(GameDifficulty difficulty, float enemySpawnRateMultiplier, float decayRateMultiplier)
    {
        Difficulty = difficulty;
        EnemySpawnRateMultiplier = enemySpawnRateMultiplier;
        DecayRateMultiplier = decayRateMultiplier;
    }

    public GameDifficulty Difficulty { get; }
    public float EnemySpawnRateMultiplier { get; }
    public float DecayRateMultiplier { get; }
}

public static class GameDifficultyManager
{
    public static GameDifficulty CurrentDifficulty { get; private set; } = GameDifficulty.Normal;

    public static GameDifficultyProfile CurrentProfile => GetProfile(CurrentDifficulty);

    public static void SetDifficulty(GameDifficulty difficulty)
    {
        CurrentDifficulty = difficulty;
    }

    public static GameDifficultyProfile GetProfile(GameDifficulty difficulty)
    {
        return difficulty switch
        {
            GameDifficulty.Easy => new GameDifficultyProfile(GameDifficulty.Easy, 0.8f, 0.75f),
            GameDifficulty.Hard => new GameDifficultyProfile(GameDifficulty.Hard, 1.25f, 1.5f),
            _ => new GameDifficultyProfile(GameDifficulty.Normal, 1f, 1.25f),
        };
    }
}
