#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

/// <summary>
/// Debug command that instantiates an enemy prefab 3 units in front of the player.
/// Usage: <c>spawn &lt;enemyId&gt;</c>
/// </summary>
public class SpawnEnemyCommand : IDebugCommand
{
    private const float SPAWN_FORWARD_DISTANCE = 3f;

    public string Id          => "spawn";
    public string Description => "spawn <enemyId> — spawns an enemy in front of the player.";

    private readonly EnemyData[] _spawnableEnemies;
    private Transform            _playerTransform;

    public SpawnEnemyCommand(EnemyData[] spawnableEnemies)
    {
        _spawnableEnemies = spawnableEnemies;
    }

    public void Execute(string[] args, DebugConsole console)
    {
        if (args.Length < 1)
        {
            console.Print("Usage: spawn <enemyId>");
            return;
        }

        if (_playerTransform == null)
        {
            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj == null)
            {
                console.Print("[SpawnEnemyCommand] Player not found in scene.");
                return;
            }
            _playerTransform = playerObj.transform;
        }

        string targetId = args[0].ToLowerInvariant();
        EnemyData foundData = null;

        if (_spawnableEnemies != null)
        {
            for (int i = 0; i < _spawnableEnemies.Length; i++)
            {
                if (_spawnableEnemies[i] != null &&
                    _spawnableEnemies[i].EnemyId.ToLowerInvariant() == targetId)
                {
                    foundData = _spawnableEnemies[i];
                    break;
                }
            }
        }

        if (foundData == null)
        {
            console.Print($"Unknown enemy id: {args[0]}");
            return;
        }

        if (foundData.EnemyPrefab == null)
        {
            console.Print($"[SpawnEnemyCommand] EnemyPrefab is not assigned on {foundData.EnemyId}.");
            return;
        }

        Vector3 spawnPos = _playerTransform.position + _playerTransform.forward * SPAWN_FORWARD_DISTANCE;
        Object.Instantiate(foundData.EnemyPrefab, spawnPos, Quaternion.identity);
        console.Print($"Spawned {args[0]}");
    }
}
#endif
