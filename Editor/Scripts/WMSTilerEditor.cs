using Unity.EditorCoroutines.Editor;
using UnityEditor;
using UnityEngine;

using GeoRef;

namespace Editor.Scripts
{
    [CustomEditor(typeof(WMSTiler))]
    public class WMSTilerEditor : UnityEditor.Editor
    {
        WMSTiler container;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            container = (WMSTiler)target;

            if (GUILayout.Button("ClearTiles"))
            {
                container.ClearTiles();
            }

            if (GUILayout.Button("MakeTiles(Debug)"))
            {
                container.Awake();
                container.RunCoroutine = r => EditorCoroutineUtility.StartCoroutine(r, container);
                if (container.LoadSettings()) container.MakeTiles();
            }
        }
    }
}
