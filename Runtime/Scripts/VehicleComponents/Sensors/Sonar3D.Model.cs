using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace VehicleComponents.Sensors
{
    public partial class Sonar3D
    {
#if UNITY_EDITOR
        [HideInInspector] public string ParityTestInputPath = "";
        [HideInInspector] public string ParityTestOutputPath = "";
        [HideInInspector] public string ModelInputExportPath = "";
#endif

        void NormalizePortableModelPath()
        {
            if (string.IsNullOrWhiteSpace(ModelOnnxPath)) return;
            ModelOnnxPath = ModelOnnxPath.Trim();
            if (!Path.IsPathRooted(ModelOnnxPath)) return;

            string fileName = Path.GetFileName(ModelOnnxPath);
            Debug.LogWarning(
                $"[{name}] ModelOnnxPath was an absolute path and has been reset to portable filename '{fileName}'. " +
                $"Place that file in Assets/StreamingAssets/.",
                this);
            ModelOnnxPath = fileName;
        }

        string ResolveModelPath()
        {
            if (string.IsNullOrWhiteSpace(ModelOnnxPath)) return null;

            string relativePath = ModelOnnxPath.Trim().Replace('\\', '/');
            if (Path.IsPathRooted(relativePath))
            {
                if (File.Exists(relativePath)) return relativePath;
                relativePath = Path.GetFileName(relativePath);
            }

            foreach (string candidate in GetModelPathCandidates(relativePath))
            {
                if (File.Exists(candidate)) return candidate;
            }

            return Path.Combine(Application.streamingAssetsPath, relativePath);
        }

        IEnumerable<string> GetModelPathCandidates(string relativePath)
        {
            yield return Path.Combine(Application.streamingAssetsPath, relativePath);
            yield return Path.Combine(Application.dataPath, relativePath);
            yield return Path.Combine(Application.dataPath, "StreamingAssets", relativePath);

            string packageModelPath = GetPackageModelPath(relativePath);
            if (!string.IsNullOrEmpty(packageModelPath))
                yield return packageModelPath;
        }

        static string GetPackageModelPath(string relativePath)
        {
#if UNITY_EDITOR
            try
            {
                var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(Sonar).Assembly);
                if (packageInfo == null || string.IsNullOrEmpty(packageInfo.resolvedPath)) return null;
                return Path.Combine(packageInfo.resolvedPath, "Runtime", "Models", "Sonar", relativePath);
            }
            catch
            {
                return null;
            }
#else
            return null;
#endif
        }

        static string GetPreferredModelPath(string modelOnnxPath)
        {
            return Path.Combine(Application.streamingAssetsPath, modelOnnxPath.Trim().Replace('\\', '/'));
        }

        void LogModelPathDiagnostics(string resolvedPath)
        {
            string streamingDir = Application.streamingAssetsPath;
            bool streamingDirExists = Directory.Exists(streamingDir);
            string streamingContents = streamingDirExists
                ? string.Join(", ", Directory.GetFiles(streamingDir).Select(Path.GetFileName))
                : "(missing)";
            string preferredPath = GetPreferredModelPath(ModelOnnxPath);

            Debug.LogError(
                $"[{name}] Sonar return model file not found.\n" +
                $"  ModelOnnxPath (inspector): '{ModelOnnxPath}'\n" +
                $"  Preferred path: '{preferredPath}'\n" +
                $"  Last resolved path: '{resolvedPath}'\n" +
                $"  StreamingAssets: '{streamingDir}'\n" +
                $"  Files in StreamingAssets: {streamingContents}\n" +
                $"  Fix: copy sonar_model.onnx to Assets/StreamingAssets/ and set ModelOnnxPath to 'sonar_model.onnx'.",
                this);
        }

        void InitReturnModel()
        {
            modelLoadAttempted = true;
            EnsureModelBuffers();
            returnModel.Dispose();
            ModelResolvedPath = ResolveModelPath() ?? "";
            ModelFileFound = File.Exists(ModelResolvedPath);
            ModelLoadError = "";
            if (!ModelFileFound)
            {
                LogModelPathDiagnostics(ModelResolvedPath);
                ModelLoadError = "Model file not found.";
                return;
            }
            try
            {
                returnModel.Load(ModelResolvedPath, ModelPreferGpu);
                ModelLoadError = returnModel.LoadMessage;
                Debug.Log($"[{name}] Sonar return model loaded from '{ModelResolvedPath}' ({returnModel.ExecutionProvider}).", this);
            }
            catch (Exception ex)
            {
                ModelLoadError = ex.Message;
                Debug.LogError($"[{name}] Sonar return model failed to load: {ModelLoadError}", this);
            }
            ModelExecutionProvider = returnModel.ExecutionProvider;
            ModelAvailableProviders = returnModel.AvailableProviders;
        }

        [ContextMenu("Retry Sonar Model Load")]
        void RetrySonarModelLoad()
        {
            modelLoadAttempted = false;
            InitReturnModel();
        }

        [ContextMenu("Validate Sonar Model Path")]
        void ValidateSonarModelPath()
        {
            NormalizePortableModelPath();
            string modelPath = ResolveModelPath();
            ModelResolvedPath = modelPath ?? "";
            if (!string.IsNullOrEmpty(modelPath) && File.Exists(modelPath))
                Debug.Log($"[{name}] Sonar model found at '{modelPath}'.", this);
            else
                LogModelPathDiagnostics(modelPath);
        }

        [ContextMenu("Reset Portable Model Path")]
        void ResetPortableModelPath()
        {
            ModelOnnxPath = "sonar_model.onnx";
            ValidateSonarModelPath();
        }

#if UNITY_EDITOR
        public void ExportModelInputBin(string path)
        {
            byte[] bytes = new byte[modelInputBuffer.Length * sizeof(float)];
            Buffer.BlockCopy(modelInputBuffer, 0, bytes, 0, bytes.Length);
            File.WriteAllBytes(path, bytes);
            Debug.Log($"Exported sonar model input tensor to '{path}' ({modelInputBuffer.Length} floats).");
        }

        [ContextMenu("Run ONNX Parity Test")]
        public void RunOnnxParityTest()
        {
            if (!modelReady)
            {
                Debug.LogError("Return model is not loaded; cannot run parity test.");
                return;
            }
            if (string.IsNullOrWhiteSpace(ParityTestInputPath) || string.IsNullOrWhiteSpace(ParityTestOutputPath))
            {
                Debug.LogError("Set ParityTestInputPath and ParityTestOutputPath to deployment testdata binaries.");
                return;
            }
            if (!File.Exists(ParityTestInputPath) || !File.Exists(ParityTestOutputPath))
            {
                Debug.LogError("Parity test files were not found on disk.");
                return;
            }

            float[] inputValues = ReadFloat32File(ParityTestInputPath);
            float[] reference = ReadFloat32File(ParityTestOutputPath);
            if (inputValues.Length != modelInputBuffer.Length)
            {
                Debug.LogError($"Parity input has {inputValues.Length} floats; expected {modelInputBuffer.Length}.");
                return;
            }

            float[] output = returnModel.Run(inputValues);

            if (output.Length != reference.Length)
            {
                Debug.LogError($"Parity output has {output.Length} floats; reference has {reference.Length}.");
                return;
            }

            double maxAbs = 0d;
            double meanAbs = 0d;
            for (int i = 0; i < reference.Length; i++)
            {
                double d = Math.Abs(reference[i] - output[i]);
                maxAbs = Math.Max(maxAbs, d);
                meanAbs += d;
            }
            meanAbs /= reference.Length;
            Debug.Log($"ONNX parity test: max abs error={maxAbs:G8}, mean abs error={meanAbs:G8}");
        }

        static float[] ReadFloat32File(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            float[] values = new float[bytes.Length / sizeof(float)];
            Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length);
            return values;
        }
#endif

    }
}
