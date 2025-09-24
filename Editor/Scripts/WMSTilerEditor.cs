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

            if (GUILayout.Button("MakeTiles(Debug)"))
            {
                container.Awake();
                container.Start();
            }
        }
    }
}