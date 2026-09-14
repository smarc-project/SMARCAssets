using System;
using UnityEngine;
using Unity.Collections;

namespace VehicleComponents.Sensors
{
    public enum SonarDetectionMode
    {
        Threshold,
        Hybrid
    }


    // Inherit the existing output contract so ROS publishers and RayViewer need no adapter.
    [AddComponentMenu("Smarc/Sensor/Sonar 3D")]
    public partial class Sonar3D : Sonar
    {
        public const int ModelImageHeight = Sonar3DModelSpec.V1.ImageHeight;
        public const int ModelImageWidth = Sonar3DModelSpec.V1.ImageWidth;
        public const int ModelPixelCount = Sonar3DModelSpec.V1.PixelCount;
        public const int ModelInputChannels = Sonar3DModelSpec.V1.InputChannels;
        public const int ModelOutputChannels = Sonar3DModelSpec.V1.OutputChannels;
        public const float ModelFovHDegFixed = 90f;
        public const float ModelFovVDegFixed = 40f;
        public const float ModelMaxRangeFixed = 15f;
        const float ModelLogEpsilon = 1f;

        [Header("ML Return Model")]
        [HideInInspector] public bool UseReturnModel = true;
        [Tooltip("Model filename relative to StreamingAssets (portable). Example: sonar_model.onnx")]
        public string ModelOnnxPath = "sonar_model.onnx";
        [HideInInspector] public float ModelFovHDeg = ModelFovHDegFixed;
        [HideInInspector] public float ModelFovVDeg = ModelFovVDegFixed;
        [HideInInspector] public float ModelMaxRange = ModelMaxRangeFixed;
        public float ModelYawOffsetDeg = 0f;
        public float ModelPitchOffsetDeg = 0f;
        [Tooltip("Detection probability at or above this value counts as a detected point.")]
        [Range(0f, 1f)]
        public float ModelDetectionThreshold = 0.5f;
        public SonarDetectionMode ModelDetectionMode = SonarDetectionMode.Threshold;
        [Tooltip("Hybrid: probabilities below this threshold always fail.")]
        [Range(0f, 1f)] public float ModelDetectionLowerThreshold = 0.25f;
        [Tooltip("Hybrid: probabilities at or above this threshold always pass. Between thresholds, sample with probability p.")]
        [Range(0f, 1f)] public float ModelDetectionUpperThreshold = 0.75f;
        [Tooltip("Try a GPU execution provider (CoreML on macOS, DirectML/CUDA on Windows). Falls back to CPU.")]
        public bool ModelPreferGpu = false;
        [HideInInspector] public float[] ModelDetection;
        [HideInInspector] public float[] ModelRange;
        [HideInInspector] public float[] ModelRangeResidual;
        [HideInInspector] public float[] ModelIntensity;
        [HideInInspector] public bool[] ModelDetected;
        [Header("ML Return Model Stats")]
        [HideInInspector] public int ModelRaycastHitCount;
        [HideInInspector] public int ModelDetectedPointCount;
        [HideInInspector] public double ModelInputBuildMs;
        [HideInInspector] public double ModelInferenceMs;
        [HideInInspector] public double ModelPostProcessMs;
        [HideInInspector] public double ModelPipelineMs;
        [HideInInspector] public double ModelInferenceMsAvg;
        [HideInInspector] public string ModelExecutionProvider = "n/a";
        [HideInInspector] public string ModelAvailableProviders = "n/a";
        [HideInInspector] public string ModelResolvedPath = "";
        [HideInInspector] public bool ModelFileFound;
        [HideInInspector] public string ModelLoadError = "";

        readonly SonarReturnModel returnModel = new SonarReturnModel();
        bool modelReady => returnModel.Ready;
        bool modelLoadAttempted;
        float[] modelInputBuffer;
        float[] modelAzimuthNorm;
        float[] modelElevationNorm;
        Vector3[] modelBeamDirsWorld;
        float[] modelInputRanges;
        Vector3[] modelRaycastPoints;
        bool[] modelRaycastValid;
        Vector3 modelCapturePosition;
        readonly System.Random modelDetectionRandom = new System.Random();
        float modelIntensityMax = 1f;
        int modelStatsSampleCount;
        (float horizontal, float vertical, float yaw, float pitch) cachedModelGeometry;

        public override int TotalRayCount => ModelPixelCount;
        public override int HorizontalResolution => ModelImageWidth;
        public override int VerticalResolution => ModelImageHeight;

        protected override void ConfigureGeometry()
        {
            Type = SonarType.FLS3D15;
            UseReturnModel = true;
            ModelFovHDeg = ModelFovHDegFixed;
            ModelFovVDeg = ModelFovVDegFixed;
            ModelMaxRange = ModelMaxRangeFixed;
            NumRaysPerBeam = ModelImageWidth;
            NumBeams = ModelImageHeight;
            MaxRange = ModelMaxRangeFixed;
            BeamBreadthDeg = ModelFovHDegFixed;
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            ModelDetectionThreshold = Mathf.Clamp01(ModelDetectionThreshold);
            ModelDetectionLowerThreshold = Mathf.Clamp01(ModelDetectionLowerThreshold);
            ModelDetectionUpperThreshold = Mathf.Clamp(ModelDetectionUpperThreshold, ModelDetectionLowerThreshold, 1f);
            ValidateDebugSettings();
            NormalizePortableModelPath();
        }

        protected override void Awake()
        {
            base.Awake();
            EnsureModelBuffers();
            EnsureOnnxSession();
        }

        protected override void PrepareScan()
        {
            ConfigureGeometry();
            EnsureModelBuffers();
            EnsureOnnxSession();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            returnModel.Dispose();
            DestroyDebugTextures();
        }

        protected override bool UpdateRayGeometryKey()
        {
            var geometry = (ModelFovHDeg, ModelFovVDeg, ModelYawOffsetDeg, ModelPitchOffsetDeg);
            bool changed = geometry != cachedModelGeometry;
            cachedModelGeometry = geometry;
            return changed;
        }

        protected override Vector3 CalculateLocalRayDirection(int i)
        {
            var (row, col) = ModelPixelFromRayIndex(i);
            float yaw = PixelToYawDeg(col) * Mathf.Deg2Rad;
            float pitch = PixelToPitchDeg(row) * Mathf.Deg2Rad;
            // Unity local axes: right, up, forward. Positive model yaw points left.
            return new Vector3(-Mathf.Cos(pitch) * Mathf.Sin(yaw), -Mathf.Sin(pitch),
                Mathf.Cos(pitch) * Mathf.Cos(yaw)).normalized;
        }

        void EnsureOnnxSession()
        {
            if (modelReady) return;
            if (!modelLoadAttempted)
                InitReturnModel();
        }

        void EnsureModelBuffers()
        {
            if (ModelDetection == null || ModelDetection.Length != ModelPixelCount
                || ModelRangeResidual == null || ModelRangeResidual.Length != ModelPixelCount)
            {
                ModelDetection = new float[ModelPixelCount];
                ModelRange = new float[ModelPixelCount];
                ModelRangeResidual = new float[ModelPixelCount];
                ModelIntensity = new float[ModelPixelCount];
                modelInputBuffer = new float[ModelInputChannels * ModelPixelCount];
                ModelDetected = new bool[ModelPixelCount];
                modelAzimuthNorm = new float[ModelPixelCount];
                modelElevationNorm = new float[ModelPixelCount];
                modelBeamDirsWorld = new Vector3[ModelPixelCount];
                modelInputRanges = new float[ModelPixelCount];
                modelRaycastPoints = new Vector3[ModelPixelCount];
                modelRaycastValid = new bool[ModelPixelCount];
                InitDebugColorBuffers();
                InitModelGeometry();
            }

            if (ModelDetectionTex == null)
                InitModelTextures();
        }

        bool IsModelOutputReady()
        {
            return ModelDetection != null && ModelDetection.Length == ModelPixelCount
                && ModelRange != null && ModelRange.Length == ModelPixelCount
                && ModelRangeResidual != null && ModelRangeResidual.Length == ModelPixelCount
                && ModelIntensity != null && ModelIntensity.Length == ModelPixelCount
                && modelBeamDirsWorld != null && modelBeamDirsWorld.Length == ModelPixelCount
                && modelInputBuffer != null && modelInputBuffer.Length == ModelInputChannels * ModelPixelCount;
        }

        void InitModelGeometry()
        {
            for (int row = 0; row < ModelImageHeight; row++)
            {
                for (int col = 0; col < ModelImageWidth; col++)
                {
                    int i = row * ModelImageWidth + col;
                    float yawDeg = PixelToYawDeg(col);
                    float pitchDeg = PixelToPitchDeg(row);
                    modelAzimuthNorm[i] = (yawDeg - ModelYawOffsetDeg) / (0.5f * ModelFovHDeg);
                    modelElevationNorm[i] = (pitchDeg - ModelPitchOffsetDeg) / (0.5f * ModelFovVDeg);
                }
            }
        }

        float PixelToYawDeg(int col)
        {
            return ((col / (ModelImageWidth - 1f)) - 0.5f) * ModelFovHDeg + ModelYawOffsetDeg;
        }

        float PixelToPitchDeg(int row)
        {
            return ((row / (ModelImageHeight - 1f)) - 0.5f) * ModelFovVDeg + ModelPitchOffsetDeg;
        }

        public static (int row, int col) ModelPixelFromRayIndex(int i)
        {
            return (i / ModelImageWidth, i % ModelImageWidth);
        }

        protected override void UpdateSonarHits(NativeArray<RaycastHit> rayResults)
        {
            HitsMinHeight = Mathf.Infinity;
            HitsMaxHeight = 0f;
            var pipelineStopwatch = System.Diagnostics.Stopwatch.StartNew();
            BuildModelInput(rayResults);
            if (modelReady)
            {
                try { ApplyReturnModel(rayResults); }
                catch (Exception ex)
                {
                    returnModel.Dispose();
                    ModelLoadError = ex.Message;
                    Debug.LogError($"[{name}] Sonar inference failed; using physics fallback. {ex.Message}", this);
                }
            }
            if (!modelReady)
            {
                var fallbackStopwatch = System.Diagnostics.Stopwatch.StartNew();
                UpdateSonarHitsPhysics(rayResults);
                UpdateFallbackDebugFromPhysics(rayResults);
                ModelInferenceMs = 0d;
                ModelPostProcessMs = fallbackStopwatch.Elapsed.TotalMilliseconds;
            }
            ModelPipelineMs = pipelineStopwatch.Elapsed.TotalMilliseconds;
            MaybeLogModelStats();
        }

        void UpdateFallbackDebugFromPhysics(NativeArray<RaycastHit> rayResults)
        {
            if (!IsModelOutputReady()) return;

            int physicsHits = 0;
            modelIntensityMax = 1f;
            for (int i = 0; i < ModelPixelCount; i++)
            {
                var hit = rayResults[i];
                bool valid = hit.collider != null;
                float rangeM = valid ? hit.distance : 0f;
                float intensity = SonarHits[i].ReturnIntensity;

                ModelDetected[i] = valid;
                ModelDetection[i] = 0f; // No inference probability is available during physics fallback.
                ModelRangeResidual[i] = float.NaN;
                ModelRange[i] = rangeM;
                ModelIntensity[i] = intensity;
                if (IsFinite(intensity)) modelIntensityMax = Mathf.Max(modelIntensityMax, intensity);
                if (valid) physicsHits++;
            }

            ModelDetectedPointCount = physicsHits;
            if (ShowModelDebugWindow) UpdateModelTextures();
        }

        void BuildModelInput(NativeArray<RaycastHit> rayResults)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            // Results belong to the previous scheduled scan, not the current sensor pose.
            modelCapturePosition = commands[0].from;
            Vector3 sensorPos = modelCapturePosition;
            int raycastHits = 0;
            for (int i = 0; i < ModelPixelCount; i++)
            {
                // v1 input layout; see Sonar3DModelSpec.V1 when deploying a new ONNX model.
                modelInputBuffer[Sonar3DModelSpec.V1.InputAzimuthChannel * ModelPixelCount + i] = modelAzimuthNorm[i];
                modelInputBuffer[Sonar3DModelSpec.V1.InputElevationChannel * ModelPixelCount + i] = modelElevationNorm[i];

                var hit = rayResults[i];
                bool valid = hit.collider != null;
                if (valid) raycastHits++;
                float rangeNorm = 0f;
                float cosIncidence = 0f;
                float rangeM = 0f;
                if (valid)
                {
                    rangeM = hit.distance;
                    rangeNorm = Mathf.Clamp(rangeM / ModelMaxRange, 0f, 1f);
                    Vector3 toSensor = (sensorPos - hit.point).normalized;
                    cosIncidence = Mathf.Clamp(Vector3.Dot(hit.normal, toSensor), -1f, 1f);
                    cosIncidence = Mathf.Max(0f, cosIncidence);
                }
                modelInputRanges[i] = rangeM;
                modelBeamDirsWorld[i] = commands[i].direction;
                modelRaycastPoints[i] = hit.point;
                modelRaycastValid[i] = valid;
                modelInputBuffer[Sonar3DModelSpec.V1.InputRangeChannel * ModelPixelCount + i] = rangeNorm;
                modelInputBuffer[Sonar3DModelSpec.V1.InputCosIncidenceChannel * ModelPixelCount + i] = cosIncidence;
            }

            ModelRaycastHitCount = raycastHits;
            ModelInputBuildMs = stopwatch.Elapsed.TotalMilliseconds;

#if UNITY_EDITOR
            if (!string.IsNullOrWhiteSpace(ModelInputExportPath))
                ExportModelInputBin(ModelInputExportPath);
#endif
        }

        void ApplyReturnModel(NativeArray<RaycastHit> rayResults)
        {
            if (!IsModelOutputReady()) return;

            var inferenceStopwatch = System.Diagnostics.Stopwatch.StartNew();
            float[] output = returnModel.Run(modelInputBuffer);
            ModelInferenceMs = inferenceStopwatch.Elapsed.TotalMilliseconds;
            UpdateInferenceAverage(ModelInferenceMs);

            var postProcessStopwatch = System.Diagnostics.Stopwatch.StartNew();
            Vector3 sensorPos = modelCapturePosition;
            modelIntensityMax = 1f;
            int detectedPoints = 0;
            for (int i = 0; i < ModelPixelCount; i++)
            {
                // v1 output layout; see Sonar3DModelSpec.V1 when deploying a new ONNX model.
                float detectionLogit = output[Sonar3DModelSpec.V1.OutputDetectionChannel * ModelPixelCount + i];
                float rangeResidual = output[Sonar3DModelSpec.V1.OutputRangeResidualChannel * ModelPixelCount + i];
                float logIntensity = output[Sonar3DModelSpec.V1.OutputLogIntensityChannel * ModelPixelCount + i];

                float detection = 1f / (1f + Mathf.Exp(-detectionLogit));
                float rangeM = modelInputRanges[i] + rangeResidual;
                float intensity = Mathf.Max(0f, Mathf.Exp(logIntensity) - ModelLogEpsilon);

                ModelDetection[i] = detection;
                ModelRangeResidual[i] = rangeResidual; // Preserve the raw model channel before masking.
                ModelRange[i] = rangeM;
                ModelIntensity[i] = intensity;
                if (IsFinite(intensity)) modelIntensityMax = Mathf.Max(modelIntensityMax, intensity);
            }

            // Normalize against the whole frame so brightness does not depend on pixel order.
            for (int i = 0; i < ModelPixelCount; i++)
            {
                float rangeM = ModelRange[i];
                // A model return requires both a physics hit and an accepted inference result.
                bool detected = modelRaycastValid[i]
                    && IsFinite(rangeM) && rangeM > 0f && rangeM <= ModelMaxRange
                    && IsFinite(ModelIntensity[i]) && SampleModelDetection(ModelDetection[i]);
                ModelDetected[i] = detected;
                if (detected) detectedPoints++;

                var hit = rayResults[i];
                hit.point = sensorPos + modelBeamDirsWorld[i] * rangeM;
                hit.distance = rangeM;
                float normalizedIntensity = Mathf.Clamp01(ModelIntensity[i] / modelIntensityMax);
                SonarHits[i].UpdateFromModel(hit, normalizedIntensity, detected);
                if (!detected) continue;
                if (hit.point.y > HitsMaxHeight && hit.point.y < 0) HitsMaxHeight = hit.point.y;
                if (hit.point.y < HitsMinHeight) HitsMinHeight = hit.point.y;
            }

            ModelDetectedPointCount = detectedPoints;
            if (ShowModelDebugWindow) UpdateModelTextures();
            ModelPostProcessMs = postProcessStopwatch.Elapsed.TotalMilliseconds;
        }

        static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        bool SampleModelDetection(float probability)
        {
            if (!IsFinite(probability)) return false;
            if (ModelDetectionMode == SonarDetectionMode.Threshold)
                return probability >= Mathf.Clamp01(ModelDetectionThreshold);

            float lower = Mathf.Clamp01(ModelDetectionLowerThreshold);
            float upper = Mathf.Clamp(ModelDetectionUpperThreshold, lower, 1f);
            if (probability < lower) return false;
            if (probability >= upper) return true;
            // Draw once per pixel per scan; every consumer uses the stored mask.
            return modelDetectionRandom.NextDouble() < probability;
        }

        string ModelDetectionRule => ModelDetectionMode == SonarDetectionMode.Threshold
            ? $"Threshold: p >= {ModelDetectionThreshold:F2}"
            : $"Hybrid: fail below {ModelDetectionLowerThreshold:F2}, sample with p, pass from {ModelDetectionUpperThreshold:F2}";

        void UpdateInferenceAverage(double inferenceMs)
        {
            modelStatsSampleCount++;
            if (modelStatsSampleCount == 1)
                ModelInferenceMsAvg = inferenceMs;
            else
            {
                const double alpha = 2.0 / 31.0;
                ModelInferenceMsAvg = ModelInferenceMsAvg * (1.0 - alpha) + inferenceMs * alpha;
            }
        }

    }
}
