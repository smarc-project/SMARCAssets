using System.IO;
using UnityEngine;
using UnityEngine.UIElements;

namespace BagReplay
{
#if UNITY_EDITOR

    using UnityEditor;
    using UnityEditor.UIElements;

    [CustomEditor(typeof(BagDataWriterAggregator))]
    public class BagDataWriterAggregatorEditor : Editor
    {
        const string WriteKey = "Smarc.BagReplay.LastWriteFolder";

        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();

            InspectorElement.FillDefaultInspector(root, serializedObject, this);
            CreateWriteButton(root);
            return root;
        }


        private void CreateWriteButton(VisualElement root)
        {
            var writer = (BagDataWriterAggregator)target;
            // Simple browse button
            var writeBtn = new Button(() =>
                {
                    var defaultPath = EditorPrefs.GetString(WriteKey, Application.dataPath);
                    string path = EditorUtility.SaveFilePanel("Save File", defaultPath, "placeholder_x-y", "csv");
                    if (!string.IsNullOrEmpty(path))
                    {
                        var folder = Path.GetDirectoryName(path);
                        if (!string.IsNullOrEmpty(folder))
                            EditorPrefs.SetString(WriteKey, folder);

                        serializedObject.ApplyModifiedProperties();
                        writer.WriteAll(path);
                    }
                })
                { text = "Write File(s)" };

            root.Add(writeBtn);
            root.Bind(serializedObject);
        }
    }
#endif
}