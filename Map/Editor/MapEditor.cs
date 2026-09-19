#if UNITY_EDITOR
using UnityEngine;
using System.Collections;
using UnityEditor;

namespace TJ.Map
{
[ExecuteInEditMode]
[CustomEditor (typeof (MapGenerator))]
[RequireComponent(typeof(MapGenerator))]
public class MapEditor : Editor
{
    public override void OnInspectorGUI()
	{
		MapGenerator mapGenerator = (MapGenerator)target;

		if(GUILayout.Button("Clear Map")){
			mapGenerator.ClearMap();
		}
		if (GUILayout.Button ("Generate Map")) {
			_ = mapGenerator.GenerateMap(1);
		}

        if (DrawDefaultInspector ()) {
		}
	}
}
}
#endif