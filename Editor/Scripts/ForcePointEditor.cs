using UnityEditor;
using UnityEngine;

using Force;

namespace Editor.Scripts
{
    [CustomEditor(typeof(ForcePoint))]
    public class ForcePointEditor : UnityEditor.Editor
    {
        ForcePoint container;

        public override void OnInspectorGUI()
        {
            container = (ForcePoint)target;
            DrawDefaultInspector();

            if (GUILayout.Button("Set Volume to Neutral"))
            {
                container.SetVolumeToNeutral();
            }
        }
    }
}