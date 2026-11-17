using System;
using System.IO;
using System.Linq;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using static VehicleComponents.Sensors.Sonar3DModelSpec.V1;

namespace VehicleComponents.Sensors
{
    // Owns ONNX resources. It returns predictions; Sonar3D owns detection decisions.
    internal sealed class SonarReturnModel : IDisposable
    {
        InferenceSession modelSession;
        string modelInputName, modelOutputName;
        public bool Ready => modelSession != null;
        public string ExecutionProvider { get; private set; } = "n/a";
        public string AvailableProviders { get; private set; } = "n/a";
        public string LoadMessage { get; private set; } = "";

        public void Load(string path, bool preferGpu)
        {
            Dispose();
            LoadMessage = "";
            try
            {
                AvailableProviders = string.Join(", ", OrtEnv.Instance().GetAvailableProviders());
                using var options = CreateModelSessionOptions(preferGpu, out string provider);
                ExecutionProvider = provider;
                bool loaded = TryCreateInferenceSession(path, options, out string message);
                if (!loaded && !provider.StartsWith("CPU", StringComparison.Ordinal))
                {
                    using var cpuOptions = new SessionOptions { GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL };
                    loaded = TryCreateInferenceSession(path, cpuOptions, out string cpuMessage);
                    message = loaded ? $"Accelerator session failed; using CPU. {message}" : cpuMessage;
                    ExecutionProvider = "CPUExecutionProvider";
                }
                if (!loaded) throw new InvalidOperationException(message);
                LoadMessage = message;
                modelInputName = modelSession.InputMetadata.Keys.First();
                modelOutputName = modelSession.OutputMetadata.Keys.First();
                ValidateModelTensor(modelSession.InputMetadata[modelInputName], InputChannels, "input");
                ValidateModelTensor(modelSession.OutputMetadata[modelOutputName], OutputChannels, "output");
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            modelSession?.Dispose();
            modelSession = null;
        }

        bool TryCreateInferenceSession(string modelPath, SessionOptions sessionOptions, out string error)
        {
            error = "";
            try
            {
                modelSession = new InferenceSession(modelPath, sessionOptions);
                return true;
            }
            catch (Exception ex)
            {
                try
                {
                    byte[] modelBytes = File.ReadAllBytes(modelPath);
                    modelSession = new InferenceSession(modelBytes, sessionOptions);
                    error = $"Loaded from bytes after path load failed: {ex.Message}";
                    return true;
                }
                catch (Exception byteEx)
                {
                    error = $"{ex.Message}\nFallback byte load: {byteEx.Message}";
                    modelSession = null;
                    return false;
                }
            }
        }

        static SessionOptions CreateModelSessionOptions(bool preferGpu, out string configuredProvider)
        {
            var sessionOptions = new SessionOptions
            {
                GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL
            };
            configuredProvider = "CPUExecutionProvider";

            if (!preferGpu)
                return sessionOptions;

            bool gpuConfigured = false;

#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            if (!gpuConfigured && TryAppendExecutionProvider(
                    () => sessionOptions.AppendExecutionProvider_CoreML(),
                    "CoreMLExecutionProvider",
                    ref configuredProvider))
                gpuConfigured = true;
#endif

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            if (!gpuConfigured && TryAppendExecutionProvider(
                    () => sessionOptions.AppendExecutionProvider_DML(0),
                    "DmlExecutionProvider",
                    ref configuredProvider))
                gpuConfigured = true;
#endif

            if (!gpuConfigured && TryAppendExecutionProvider(
                    () => sessionOptions.AppendExecutionProvider_CUDA(0),
                    "CUDAExecutionProvider",
                    ref configuredProvider))
                gpuConfigured = true;

            if (!gpuConfigured)
                configuredProvider = "CPUExecutionProvider (GPU requested, unavailable)";

            return sessionOptions;
        }

        static bool TryAppendExecutionProvider(
            Action append,
            string providerName,
            ref string configuredProvider)
        {
            try
            {
                append();
                configuredProvider = providerName;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        static void ValidateModelTensor(NodeMetadata metadata, int channels, string label)
        {
            int[] expected = { 1, channels, ImageHeight, ImageWidth };
            if (metadata.ElementType != typeof(float) || !metadata.Dimensions.SequenceEqual(expected))
                throw new InvalidDataException($"Sonar model {label} must be float32 [{string.Join(",", expected)}].");
        }

        public float[] Run(float[] inputValues)
        {
            if (!Ready) throw new InvalidOperationException("Sonar return model is not loaded.");
            if (inputValues.Length != InputChannels * PixelCount)
                throw new ArgumentException("Unexpected sonar input length.", nameof(inputValues));
            var inputTensor = new DenseTensor<float>(
                inputValues,
                new[] {1, InputChannels, ImageHeight, ImageWidth});
            var inputs = new[]
            {
                NamedOnnxValue.CreateFromTensor(modelInputName, inputTensor)
            };
            using var results = modelSession.Run(inputs);
            float[] output = results.First(value => value.Name == modelOutputName).AsTensor<float>().ToArray();
            if (output.Length != OutputChannels * PixelCount)
                throw new InvalidDataException($"Unexpected sonar output length: {output.Length}.");
            return output;
        }

    }
}
