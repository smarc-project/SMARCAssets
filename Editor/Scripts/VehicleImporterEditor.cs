using UnityEditor;
using UnityEngine;

namespace Editor.Scripts
{
    [CustomEditor(typeof(VehicleImporter))]
    public class VehicleImporterEditor : UnityEditor.Editor
    {
        public string _filePath = "";
        private VehicleImporter _vehicleImporter;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            _vehicleImporter = (VehicleImporter) target;

            if (GUILayout.Button("Load"))
            {
                _filePath = EditorUtility.OpenFilePanel("Select vehicle model file", _filePath, "json");
                if (_filePath is {Length: > 0})
                {
                    _vehicleImporter.LoadJson(_filePath);
                }
            }

            if (GUILayout.Button("Save"))
            {
                var split = _filePath.Split("/");
                _filePath = EditorUtility.SaveFilePanel("Select vehicle model file", _filePath, split[split.Length - 1], "json");
                if (_filePath is {Length: > 0})
                {
                    _vehicleImporter.SaveJson(_filePath);
                }
            }
        }
    }
}