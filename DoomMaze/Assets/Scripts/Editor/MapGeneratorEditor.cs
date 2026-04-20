using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MapGenerator))]
public class MapGeneratorEditor : Editor
{
    /// <summary>
    /// Draws the default inspector fields plus Generate Map and Populate buttons.
    /// </summary>
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();

        MapGenerator generator = (MapGenerator)target;

        if (GUILayout.Button("Generate Map", GUILayout.Height(32)))
        {
            generator.Generate();
            generator.PrintMap();
        }

        EditorGUILayout.Space();

        MazePopulator populator = generator.GetComponent<MazePopulator>();
        if (populator != null)
        {
            if (GUILayout.Button("Populate Maze", GUILayout.Height(32)))
            {
                generator.Generate();
                populator.Populate();
            }

            if (GUILayout.Button("Clear Rooms", GUILayout.Height(24)))
                populator.ClearSpawnedRooms();
        }
        else
        {
            EditorGUILayout.HelpBox(
                "Add a MazePopulator component to enable in-editor population.",
                MessageType.Info);
        }
    }
}
