using UnityEditor;
using UnityEngine;

using dji;

namespace Editor.Scripts
{
    [CustomEditor(typeof(DJIController))]
    public class DJIControllerEditor : UnityEditor.Editor
    {
        DJIController container;

        public override void OnInspectorGUI()
        {
            container = (DJIController)target;
            DrawDefaultInspector();

            if (GUILayout.Button("TakeOff"))
            {
                container.TakeOff();
            }
            
            if (GUILayout.Button("Land"))
            {
                container.Land();
            }
        }
    }
}