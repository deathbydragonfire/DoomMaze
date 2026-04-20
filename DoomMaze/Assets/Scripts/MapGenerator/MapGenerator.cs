using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class MapGenerator : MonoBehaviour
{
    public enum RoomType { Start, Enemy, Hallway, Upgrade, Boss, Exit, DeadEnd }

    public class RoomNode
    {
        public RoomType Type;
        public int ConnectionCount;
        public int Depth;
        public int PathEnemyCount;
        public List<RoomNode> Children = new();

        public RoomNode(RoomType type, int connectionCount, int depth)
        {
            Type = type;
            ConnectionCount = connectionCount;
            Depth = depth;
        }
    }

    [Header("Generation Settings")]
    [SerializeField] private int seed = 0;
    [SerializeField] private bool useRandomSeed = true;
    [SerializeField] private int minDepth = 5;
    [SerializeField] private int maxDepth = 10;
    [SerializeField] private int upgradeRoomQuota = 2;

    [Header("Hallway Connection Weights")]
    [SerializeField] private float hallwayTwoConnectionWeight   = 1f;
    [SerializeField] private float hallwayThreeConnectionWeight = 1f;
    [SerializeField] private float hallwayFourConnectionWeight  = 1f;

    private const int MinEnemiesOnPathBeforeBoss = 3;
    private const float BossChanceAfterThreshold = 0.25f;
    private const float DeadEndChanceAfterEnemy = 0.4f;
    private const float UpgradeChanceAfterEnemy = 0.5f;

    private RoomNode _root;
    private int _enemyRoomCount;
    private int _upgradeRoomCount;
    private bool _bossGenerated;
    private System.Random _rng;

    private void Start()
    {
        Generate();
        PrintMap();
    }

    /// <summary>
    /// Runs the full map generation from scratch.
    /// </summary>
    public void Generate()
    {
        int resolvedSeed = useRandomSeed ? Random.Range(int.MinValue, int.MaxValue) : seed;
        _rng = new System.Random(resolvedSeed);
        _enemyRoomCount = 0;
        _upgradeRoomCount = 0;
        _bossGenerated = false;

        Debug.Log($"[MapGenerator] Generating with seed: {resolvedSeed}");

        _root = new RoomNode(RoomType.Start, connectionCount: 1, depth: 0);
        BuildChildren(_root, pathEnemyCount: 0);
        EnsureBossGenerated();
        EnsureUpgradeQuota();
    }

    private void BuildChildren(RoomNode parent, int pathEnemyCount)
    {
        int exits = parent.Type == RoomType.Start ? 1 : parent.ConnectionCount - 1;

        for (int i = 0; i < exits; i++)
        {
            RoomNode child = CreateNextRoom(parent, pathEnemyCount);
            parent.Children.Add(child);

            int childPathEnemyCount = pathEnemyCount + (child.Type == RoomType.Enemy ? 1 : 0);
            child.PathEnemyCount = childPathEnemyCount;

            bool isTerminal = child.Type == RoomType.Exit || child.Type == RoomType.DeadEnd;
            if (!isTerminal)
                BuildChildren(child, childPathEnemyCount);
        }
    }

    private RoomNode CreateNextRoom(RoomNode parent, int pathEnemyCount)
    {
        RoomType nextType = PickNextRoomType(parent.Type, parent.Depth, pathEnemyCount);
        int connections = PickConnectionCount(nextType);
        int depth = parent.Depth + 1;

        if (nextType == RoomType.Enemy)
            _enemyRoomCount++;

        if (nextType == RoomType.Upgrade)
            _upgradeRoomCount++;

        if (nextType == RoomType.Boss)
            _bossGenerated = true;

        return new RoomNode(nextType, connections, depth);
    }

    private RoomType PickNextRoomType(RoomType parentType, int parentDepth, int pathEnemyCount)
    {
        // Boss always leads to Exit — never intercept this.
        if (parentType == RoomType.Boss)
            return RoomType.Exit;

        // Max depth cap: reserve last 2 slots for Boss + Exit on the main path,
        // or a DeadEnd on branches where the boss is already placed.
        if (parentDepth >= maxDepth - 2)
        {
            if (!_bossGenerated && pathEnemyCount >= MinEnemiesOnPathBeforeBoss)
                return RoomType.Boss;
            return RoomType.DeadEnd;
        }

        return parentType switch
        {
            RoomType.Start   => RoomType.Enemy,
            RoomType.Enemy   => PickEnemyNext(parentDepth),
            RoomType.Hallway => PickHallwayNext(pathEnemyCount),
            RoomType.Upgrade => Roll(0.5f) ? RoomType.Hallway : RoomType.Enemy,
            _                => RoomType.DeadEnd,
        };
    }

    private RoomType PickEnemyNext(int currentDepth)
    {
        // Suppress dead ends until the minimum depth is reached.
        if (currentDepth >= minDepth && Roll(DeadEndChanceAfterEnemy))
            return RoomType.DeadEnd;

        // Suppress upgrades once the quota is met.
        if (_upgradeRoomCount < upgradeRoomQuota && Roll(UpgradeChanceAfterEnemy))
            return RoomType.Upgrade;

        return RoomType.Hallway;
    }

    private RoomType PickHallwayNext(int pathEnemyCount)
    {
        // Boss is gated: only one may exist and the path must have enough enemies.
        if (!_bossGenerated && pathEnemyCount >= MinEnemiesOnPathBeforeBoss && Roll(BossChanceAfterThreshold))
            return RoomType.Boss;

        return RoomType.Enemy;
    }

    private int PickConnectionCount(RoomType type)
    {
        return type switch
        {
            RoomType.Start   => 1,
            RoomType.Enemy   => 2,
            RoomType.Upgrade => 2,
            RoomType.Boss    => 2,
            RoomType.Exit    => 1,
            RoomType.DeadEnd => 1,
            RoomType.Hallway => PickHallwayConnectionCount(),
            _                => 1,
        };
    }

    private bool Roll(float chance) => (float)_rng.NextDouble() < chance;

    private void EnsureBossGenerated()
    {
        if (_bossGenerated) return;

        var deadEnds = new List<RoomNode>();
        CollectDeadEnds(_root, deadEnds);

        if (deadEnds.Count == 0)
        {
            Debug.LogWarning("[MapGenerator] No boss generated and no dead ends available to replace.");
            return;
        }

        RoomNode chosen = deadEnds[_rng.Next(deadEnds.Count)];
        chosen.Type = RoomType.Boss;
        chosen.ConnectionCount = 2;
        chosen.Children.Add(new RoomNode(RoomType.Exit, connectionCount: 1, chosen.Depth + 1));
        _bossGenerated = true;
    }

    private void CollectDeadEnds(RoomNode node, List<RoomNode> results)
    {
        if (node.Type == RoomType.DeadEnd)
        {
            results.Add(node);
            return;
        }

        foreach (RoomNode child in node.Children)
            CollectDeadEnds(child, results);
    }

    private int PickHallwayConnectionCount()
    {
        float total = hallwayTwoConnectionWeight + hallwayThreeConnectionWeight + hallwayFourConnectionWeight;
        float roll = (float)_rng.NextDouble() * total;

        if (roll < hallwayTwoConnectionWeight)
            return 2;
        if (roll < hallwayTwoConnectionWeight + hallwayThreeConnectionWeight)
            return 3;
        return 4;
    }

    private void EnsureUpgradeQuota()
    {
        int needed = upgradeRoomQuota - _upgradeRoomCount;
        if (needed <= 0) return;

        // First: insert one upgrade immediately before the boss.
        if (InsertUpgradeBefore(RoomType.Boss, pickRandom: false))
            needed--;

        // Remaining: insert upgrades before randomly chosen dead ends.
        while (needed > 0)
        {
            if (!InsertUpgradeBefore(RoomType.DeadEnd, pickRandom: true))
                break;
            needed--;
        }

        if (needed > 0)
            Debug.LogWarning($"[MapGenerator] Upgrade quota unmet — {needed} upgrade(s) could not be placed.");
    }

    /// <summary>
    /// Inserts an Upgrade node as the direct parent of the first (or random) node of targetType.
    /// Returns true if a suitable location was found and the insertion succeeded.
    /// </summary>
    private bool InsertUpgradeBefore(RoomType targetType, bool pickRandom)
    {
        var candidates = new List<(RoomNode parent, int childIndex)>();
        CollectParentsOf(targetType, _root, candidates);

        if (candidates.Count == 0) return false;

        var (parent, idx) = pickRandom
            ? candidates[_rng.Next(candidates.Count)]
            : candidates[0];

        RoomNode target = parent.Children[idx];
        var upgrade = new RoomNode(RoomType.Upgrade, connectionCount: 2, target.Depth);
        upgrade.Children.Add(target);
        ShiftDepths(target, target.Depth + 1);
        parent.Children[idx] = upgrade;
        _upgradeRoomCount++;
        return true;
    }

    private void CollectParentsOf(RoomType targetType, RoomNode node, List<(RoomNode, int)> results)
    {
        for (int i = 0; i < node.Children.Count; i++)
        {
            if (node.Children[i].Type == targetType)
                results.Add((node, i));
            else
                CollectParentsOf(targetType, node.Children[i], results);
        }
    }

    private void ShiftDepths(RoomNode node, int depth)
    {
        node.Depth = depth;
        foreach (RoomNode child in node.Children)
            ShiftDepths(child, depth + 1);
    }

    // -------------------------------------------------------------------------
    // Debug printing
    // -------------------------------------------------------------------------

    /// <summary>
    /// Prints the generated map tree to the Unity console in a readable format.
    /// </summary>
    public void PrintMap()
    {
        if (_root == null)
        {
            Debug.LogWarning("[MapGenerator] No map generated yet.");
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine("[MapGenerator] Generated map layout:");
        AppendNode(sb, _root, prefix: "", isLast: true);
        sb.AppendLine($"\nTotal enemy rooms: {_enemyRoomCount}");
        sb.AppendLine($"Total upgrade rooms: {_upgradeRoomCount} / {upgradeRoomQuota}");
        sb.AppendLine($"Boss generated: {_bossGenerated}");
        Debug.Log(sb.ToString());
    }

    private void AppendNode(StringBuilder sb, RoomNode node, string prefix, bool isLast)
    {
        string connector = isLast ? "└── " : "├── ";

        string detail = node.Type switch
        {
            RoomType.Hallway => $" [{node.ConnectionCount} connections]",
            RoomType.Boss    => $" (enemies on path: {node.PathEnemyCount})",
            RoomType.DeadEnd => " (terminal)",
            _                => string.Empty,
        };

        sb.AppendLine($"{prefix}{connector}[{node.Type}]{detail}");

        string childPrefix = prefix + (isLast ? "    " : "│   ");

        for (int i = 0; i < node.Children.Count; i++)
        {
            bool childIsLast = i == node.Children.Count - 1;
            AppendNode(sb, node.Children[i], childPrefix, childIsLast);
        }
    }
}
