using System.IO;
using UnityEngine;
using UnityEngine.UIElements;

namespace BagReplay
{
#if UNITY_EDITOR

    using UnityEditor;
    using UnityEditor.UIElements;

    [CustomEditor(typeof(BagDataWriter))]
    public class BagDataWriterEditor : Editor
    {
        const string WriteKey = "Smarc.BagReplay.LastWriteFolder";
        const string BrowseKey = "Smarc.BagReplay.LastBrowseFolder";

        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();

            InspectorElement.FillDefaultInspector(root, serializedObject, this);
            CreateBrowseButton(root);
            CreateWriteButton(root);
            return root;
        }

        private void CreateBrowseButton(VisualElement root)
        {
            var pathField = new TextField("Source Bag Path") { isReadOnly = true, focusable = false };
            pathField.bindingPath = "sourceFilePath";
            root.Add(pathField);

            // Simple browse button
            var browseBtn = new Button(() =>
                {
                    var defaultPath = EditorPrefs.GetString(BrowseKey, Application.dataPath);
                    string path = EditorUtility.OpenFilePanel("Select File", defaultPath, "db3");
                    if (!string.IsNullOrEmpty(path))
                    {
                        var folder = Path.GetDirectoryName(path);
                        if (!string.IsNullOrEmpty(folder))
                            EditorPrefs.SetString(BrowseKey, folder);

                        var sp = serializedObject.FindProperty("sourceFilePath");
                        sp.stringValue = path;
                        serializedObject.ApplyModifiedProperties();
                    }
                })
                { text = "Browse…" };

            root.Add(browseBtn);
            root.Bind(serializedObject);
        }

        private void CreateWriteButton(VisualElement root)
        {
            var writer = (BagDataWriter)target;
            // Simple browse button
            var writeBtn = new Button(() =>
                {
                    var defaultPath = EditorPrefs.GetString(WriteKey, Application.dataPath);
                    string path = EditorUtility.SaveFilePanel("Save File", defaultPath, Path.GetFileNameWithoutExtension(writer.sourceFilePath), "csv");
                    if (!string.IsNullOrEmpty(path))
                    {
                        var folder = Path.GetDirectoryName(path);
                        if (!string.IsNullOrEmpty(folder))
                            EditorPrefs.SetString(WriteKey, folder);

                        serializedObject.ApplyModifiedProperties();
                        writer.WriteFile(path);
                    }
                })
                { text = "Write File(s)" };

            root.Add(writeBtn);
            root.Bind(serializedObject);
        }
    }
#endif
}