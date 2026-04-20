using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MapGenerator))]
public class MapGeneratorEditor : Editor
{
    /// <summary>
    /// Draws the default inspector fields plus a Generate Map button.
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
    }
}
