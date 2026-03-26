using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(MapGenerator))]
public class MapGeneratorEditor : Editor
{
    
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        MapGenerator mapGenerator = (MapGenerator)target;

        GUILayout.BeginHorizontal();

        if(GUILayout.Button("Generate Map"))
        {
            mapGenerator.DeleteMap();
            mapGenerator.GenerateMap();
        }

        if(GUILayout.Button("Delete Map"))
        {
            mapGenerator.DeleteMap();
        }

        GUILayout.EndHorizontal();
    }
}
