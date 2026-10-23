using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VehicleComponents.Sensors;

[CustomEditor(typeof(Sonar), true)]
public class SonarEditor : UnityEditor.Editor
{
    // Inherited Sonar beam/SSS settings are serialized for compatibility but hidden on Sonar3D.
    static readonly string[] InheritedSonarSettings =
    {
        "m_Script", "Type", "NumRaysPerBeam", "NumBeams", "MaxRange", "BeamBreadthDeg",
        "BeamBreadth3DecibelsDeg", "TiltAngleDeg", "FLSFOVDeg", "NumBucketsPerBeam",
        "isInterferometric", "MultGain", "UseAdditiveNoise", "AddNoiseStd", "AddNoiseMean"
    };

    public override void OnInspectorGUI()
    {
        if (target is Sonar3D)
        {
            EditorGUILayout.LabelField("3D sonar", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("256 × 64 rays • 90° × 40° • 15 m range");
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, InheritedSonarSettings);
            serializedObject.ApplyModifiedProperties();
            return;
        }

        DrawDefaultInspector();
        var sonar = (Sonar)target;
        if (!ShouldOffer3DConversion(sonar)) return;
        EditorGUILayout.HelpBox(
            "3D sonar uses the dedicated Sonar3D component. Convert in place to keep publisher references.",
            MessageType.Warning);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
        {
            if (GUILayout.Button("Convert to Sonar3D"))
            {
                ConvertTo3D((Sonar)target);
                GUIUtility.ExitGUI();
            }
        }
    }

    [MenuItem("CONTEXT/Sonar/Convert to Sonar3D")]
    static void ConvertContext(MenuCommand command) => ConvertTo3D((Sonar)command.context);

    [MenuItem("CONTEXT/Sonar/Convert to Sonar3D", true)]
    static bool CanConvert(MenuCommand command) =>
        !EditorApplication.isPlaying && command.context is Sonar sonar && sonar.GetType() == typeof(Sonar);

    static bool ShouldOffer3DConversion(Sonar sonar)
    {
        if (sonar.Type == SonarType.FLS3D15) return true;
        return sonar.linkName.Contains("3d_sonar", System.StringComparison.OrdinalIgnoreCase)
            || sonar.gameObject.name.Contains("3d_sonar", System.StringComparison.OrdinalIgnoreCase);
    }

    static void ConvertTo3D(Sonar sonar)
    {
        MonoScript script = AssetDatabase.FindAssets("Sonar3D t:MonoScript")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<MonoScript>)
            .First(candidate => candidate.GetClass() == typeof(Sonar3D));
        Undo.RegisterCompleteObjectUndo(sonar, "Convert sonar to 3D");
        var serialized = new SerializedObject(sonar);
        // Replace the script in place so the component's file ID and common settings survive.
        serialized.FindProperty("m_Script").objectReferenceValue = script;
        serialized.FindProperty("m_Enabled").boolValue = true;
        serialized.ApplyModifiedProperties();

        EditorUtility.SetDirty(sonar);

        var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
        if (prefabStage != null)
        {
            // Prefab Mode does not auto-save script swaps; write the open prefab asset explicitly.
            EditorUtility.SetDirty(prefabStage.prefabContentsRoot);
            PrefabUtility.SaveAsPrefabAsset(prefabStage.prefabContentsRoot, prefabStage.assetPath);
            return;
        }

        if (PrefabUtility.IsPartOfPrefabInstance(sonar))
            PrefabUtility.RecordPrefabInstancePropertyModifications(sonar);
    }
}
