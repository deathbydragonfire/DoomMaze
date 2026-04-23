using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MazeOverlapTester))]
public class MazeOverlapTesterEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();

        MazeOverlapTester tester = (MazeOverlapTester)target;

        if (GUILayout.Button("Run Overlap Tests", GUILayout.Height(32)))
        {
            tester.RunTests();
            EditorUtility.SetDirty(tester);
        }

        if (tester.TotalRuns <= 0)
            return;

        EditorGUILayout.Space();

        // --- Overlap results ---
        EditorGUILayout.LabelField("Overlap", EditorStyles.boldLabel);
        DrawResultsBar(
            label:    tester.FailCount == 0
                          ? $"✓  All {tester.TotalRuns} runs overlap-free"
                          : $"✗  {tester.FailCount} / {tester.TotalRuns} runs had overlaps",
            passed:   tester.FailCount == 0,
            passRate: tester.PassRate);

        EditorGUILayout.Space();

        // --- Criteria results ---
        EditorGUILayout.LabelField("Criteria", EditorStyles.boldLabel);

        bool criteriaAllPassed = tester.BossMissingCount == 0 && tester.UpgradesMismatchCount == 0;
        DrawResultsBar(
            label:    criteriaAllPassed
                          ? $"✓  All {tester.TotalRuns} runs met criteria"
                          : $"✗  {tester.TotalRuns - tester.CriteriaPassCount} / {tester.TotalRuns} runs failed criteria",
            passed:   criteriaAllPassed,
            passRate: tester.CriteriaPassRate);

        if (!criteriaAllPassed)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            if (tester.BossMissingCount > 0)
                DrawCriteriaRow("Boss room missing", tester.BossMissingCount, tester.TotalRuns, false);

            if (tester.UpgradesMismatchCount > 0)
                DrawCriteriaRow("Upgrade prefab mismatch", tester.UpgradesMismatchCount, tester.TotalRuns, false);

            EditorGUILayout.EndVertical();
        }
    }

    private static void DrawResultsBar(string label, bool passed, float passRate)
    {
        GUIStyle labelStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            normal = { textColor = passed ? new Color(0.2f, 0.8f, 0.2f) : new Color(1f, 0.4f, 0.2f) }
        };

        EditorGUILayout.LabelField(label, labelStyle);

        Rect barRect = GUILayoutUtility.GetRect(0, 18, GUILayout.ExpandWidth(true));
        float fill = passRate / 100f;

        EditorGUI.DrawRect(barRect, new Color(0.2f, 0.2f, 0.2f));
        EditorGUI.DrawRect(
            new Rect(barRect.x, barRect.y, barRect.width * fill, barRect.height),
            passed ? new Color(0.2f, 0.7f, 0.2f) : new Color(0.8f, 0.4f, 0.1f));

        EditorGUI.LabelField(barRect, $"  {passRate:F1}% pass rate", EditorStyles.miniLabel);
    }

    private static void DrawCriteriaRow(string criterionName, int failedRuns, int totalRuns, bool passed)
    {
        GUIStyle style = new GUIStyle(EditorStyles.label)
        {
            normal = { textColor = passed ? new Color(0.2f, 0.8f, 0.2f) : new Color(1f, 0.4f, 0.2f) }
        };
        EditorGUILayout.LabelField($"  {criterionName}: {failedRuns} / {totalRuns} runs", style);
    }
}
