using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Runs the map generator and populator back-to-back for a configurable number of
/// iterations and reports how many layouts were produced cleanly vs. with overlaps,
/// and whether each map satisfies the required maze criteria (boss room and upgrades).
/// Attach to the same GameObject as MapGenerator and MazePopulator.
/// Trigger via the Inspector button (Edit Mode) or the component context menu.
/// </summary>
[RequireComponent(typeof(MapGenerator))]
[RequireComponent(typeof(MazePopulator))]
public class MazeOverlapTester : MonoBehaviour
{
    private const string BossPrefabName    = "50_2_room_100_100_tall";
    private const string UpgradePrefabName = "50_2_straight_hallway_round";

    [Tooltip("Number of maps to generate and test.")]
    [SerializeField] private int testCount = 50;

    [Tooltip("Two rooms are considered overlapping if their pivot positions are " +
             "closer than this distance. Set it to just under the width of one room.")]
    [SerializeField] private float overlapDistanceThreshold = 8f;

    [Tooltip("Log details of every overlapping pair found, not just the summary.")]
    [SerializeField] private bool logOverlapDetails = true;

    // -------------------------------------------------------------------------
    // Results — written after each test run, visible in the Inspector
    // -------------------------------------------------------------------------

    [Header("Last Run Results — Overlap")]
    [SerializeField] private int totalRuns;
    [SerializeField] private int passCount;
    [SerializeField] private int failCount;
    [SerializeField] [Range(0f, 100f)] private float passRate;

    [Header("Last Run Results — Criteria")]
    [SerializeField] private int criteriaPassCount;
    [SerializeField] private int bossMissingCount;
    [SerializeField] private int upgradesMismatchCount;
    [SerializeField] [Range(0f, 100f)] private float criteriaPassRate;

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>Total maps tested in the last run.</summary>
    public int TotalRuns => totalRuns;

    /// <summary>Maps produced without any overlapping rooms.</summary>
    public int PassCount => passCount;

    /// <summary>Maps that contained at least one overlapping room pair.</summary>
    public int FailCount => failCount;

    /// <summary>Pass rate of the last run (0–100).</summary>
    public float PassRate => passRate;

    /// <summary>Maps that satisfied all maze criteria (boss + upgrades).</summary>
    public int CriteriaPassCount => criteriaPassCount;

    /// <summary>Maps where the boss room prefab was absent or wrong.</summary>
    public int BossMissingCount => bossMissingCount;

    /// <summary>Maps where at least one upgrade room used the wrong prefab.</summary>
    public int UpgradesMismatchCount => upgradesMismatchCount;

    /// <summary>Criteria pass rate of the last run (0–100).</summary>
    public float CriteriaPassRate => criteriaPassRate;

    /// <summary>
    /// Generates and populates <see cref="testCount"/> maps, checking each for
    /// overlaps and required maze criteria. Results are written to the serialized
    /// fields so they appear in the Inspector. The last generated map is left in
    /// the scene for inspection.
    /// </summary>
    [ContextMenu("Run Overlap Tests")]
    public void RunTests()
    {
        MapGenerator  generator = GetComponent<MapGenerator>();
        MazePopulator populator = GetComponent<MazePopulator>();

        totalRuns             = testCount;
        passCount             = 0;
        failCount             = 0;
        passRate              = 0f;
        criteriaPassCount     = 0;
        bossMissingCount      = 0;
        upgradesMismatchCount = 0;
        criteriaPassRate      = 0f;

        var failingOverlaps  = new List<(int run, List<MazePopulator.OverlapPair> overlaps)>();
        var failingCriteria  = new List<(int run, bool bossOk, bool upgradesOk)>();

        Debug.Log($"[MazeOverlapTester] Starting {testCount} test run(s)...");

        for (int i = 0; i < testCount; i++)
        {
            generator.Generate();
            populator.Populate();

            // --- Overlap check ---
            List<MazePopulator.OverlapPair> overlaps = populator.FindOverlaps(overlapDistanceThreshold);
            if (overlaps.Count == 0)
                passCount++;
            else
            {
                failCount++;
                failingOverlaps.Add((i + 1, overlaps));
            }

            // --- Criteria check ---
            bool bossOk     = populator.HasPlacedRoomOfType(MapGenerator.RoomType.Boss, BossPrefabName);
            bool upgradesOk = populator.AllPlacedRoomsOfTypeMatch(MapGenerator.RoomType.Upgrade, UpgradePrefabName);

            if (bossOk && upgradesOk)
            {
                criteriaPassCount++;
            }
            else
            {
                if (!bossOk)     bossMissingCount++;
                if (!upgradesOk) upgradesMismatchCount++;
                failingCriteria.Add((i + 1, bossOk, upgradesOk));
            }
        }

        passRate         = testCount > 0 ? passCount         / (float)testCount * 100f : 0f;
        criteriaPassRate = testCount > 0 ? criteriaPassCount / (float)testCount * 100f : 0f;

        LogSummary(failingOverlaps, failingCriteria);
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private void LogSummary(
        List<(int run, List<MazePopulator.OverlapPair> overlaps)> overlapFailures,
        List<(int run, bool bossOk, bool upgradesOk)>             criteriaFailures)
    {
        var sb = new StringBuilder();
        sb.AppendLine("[MazeOverlapTester] ── Test Complete ──────────────────");
        sb.AppendLine($"  Runs   : {totalRuns}");

        sb.AppendLine("\n  [Overlap]");
        sb.AppendLine($"    Passed : {passCount}  ({passRate:F1}%)");
        sb.AppendLine($"    Failed : {failCount}");

        if (overlapFailures.Count > 0)
        {
            sb.AppendLine("\n  Overlap failing runs:");
            foreach (var (run, overlaps) in overlapFailures)
            {
                sb.AppendLine($"    Run {run,3}: {overlaps.Count} overlapping pair(s)");

                if (logOverlapDetails)
                {
                    foreach (MazePopulator.OverlapPair pair in overlaps)
                        sb.AppendLine($"             [{pair.TypeA}] {pair.RoomA}  ↔  [{pair.TypeB}] {pair.RoomB}  (dist: {pair.Distance:F2})");
                }
            }
        }

        sb.AppendLine("\n  [Criteria]");
        sb.AppendLine($"    Boss prefab    : {BossPrefabName}");
        sb.AppendLine($"    Upgrade prefab : {UpgradePrefabName}");
        sb.AppendLine($"    Passed         : {criteriaPassCount}  ({criteriaPassRate:F1}%)");
        sb.AppendLine($"    Boss missing   : {bossMissingCount}");
        sb.AppendLine($"    Upgrades wrong : {upgradesMismatchCount}");

        if (criteriaFailures.Count > 0)
        {
            sb.AppendLine("\n  Criteria failing runs:");
            foreach (var (run, bossOk, upgradesOk) in criteriaFailures)
            {
                var issues = new List<string>();
                if (!bossOk)     issues.Add($"boss missing ({BossPrefabName})");
                if (!upgradesOk) issues.Add($"upgrade prefab mismatch ({UpgradePrefabName})");
                sb.AppendLine($"    Run {run,3}: {string.Join(", ", issues)}");
            }
        }

        sb.AppendLine("  ──────────────────────────────────────────");

        bool anyFailure = failCount > 0 || criteriaFailures.Count > 0;
        if (anyFailure)
            Debug.LogWarning(sb.ToString());
        else
            Debug.Log(sb.ToString());
    }
}
