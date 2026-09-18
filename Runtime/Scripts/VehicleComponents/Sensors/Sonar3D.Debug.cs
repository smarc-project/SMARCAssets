using UnityEngine;

namespace VehicleComponents.Sensors
{
    public partial class Sonar3D
    {
        [Header("3D Sonar Debug")]
        [Tooltip("Draw raw raycast hits and accepted model returns when selected.")]
        public bool DrawModelDebug = true;
        public Color ModelRaycastHitColor = Color.white;
        [Tooltip("Show a draggable, scrollable window with heatmaps and all model statistics.")]
        public bool ShowModelDebugWindow = true;
        [Tooltip("Heatmap scale inside the debug popup.")]
        [Range(0.25f, 4f)]
        public float ModelDebugWindowScale = 1f;
        [Tooltip("0 = probability, 1 = range comparison, 2 = intensity, 3 = all, 4 = raycasts, 5 = detection mask.")]
        public int ModelDebugView = 3;
        [Tooltip("Residual heatmap scale in metres (negative to positive). Colors saturate outside this range; hover shows exact values.")]
        [Min(0.01f)] public float ModelDebugResidualRange = 1f;
        [Tooltip("Log detection counts and timings to the console about once per second.")]
        public bool ModelLogStats = false;
        [Tooltip("Gizmo subsample stride in pixels (1 = every pixel).")]
        public int ModelGizmoStride = 4;
        [HideInInspector] public Texture2D ModelRaycastTex;
        [HideInInspector] public Texture2D ModelDetectionMaskTex;
        [HideInInspector] public Texture2D ModelDetectionTex;
        [HideInInspector] public Texture2D ModelRangeTex;
        [HideInInspector] public Texture2D ModelRaycastRangeTex;
        [HideInInspector] public Texture2D ModelRangeResidualTex;
        [HideInInspector] public Texture2D ModelIntensityTex;

        Color[] modelDetectionColors, modelRangeColors, modelIntensityColors, modelRaycastColors, modelMaskColors;
        Color[] modelRaycastRangeColors, modelRangeResidualColors;
        float modelNextLogTime;
        [SerializeField] Rect modelDebugWindowRect = new Rect(20f, 20f, 640f, 720f);
        Vector2 modelDebugScroll;
        Texture2D modelDebugBackground;
        GUIStyle modelDebugWindowStyle, modelDebugLabelStyle, modelDebugHeadingStyle;
        int modelDebugHoverCol = -1;
        int modelDebugHoverRow = -1;
        const int ModelDebugWindowBaseId = 0x534F4E41;

        void ValidateDebugSettings()
        {
            ModelGizmoStride = Mathf.Max(1, ModelGizmoStride);
            ModelDebugWindowScale = Mathf.Clamp(ModelDebugWindowScale, 0.25f, 4f);
            ModelDebugResidualRange = Mathf.Max(0.01f, ModelDebugResidualRange);
            ModelDebugView = Mathf.Clamp(ModelDebugView, 0, 5);
        }

        void InitDebugColorBuffers()
        {
            modelRaycastColors = new Color[ModelPixelCount];
            modelMaskColors = new Color[ModelPixelCount];
            modelDetectionColors = new Color[ModelPixelCount];
            modelRangeColors = new Color[ModelPixelCount];
            modelRaycastRangeColors = new Color[ModelPixelCount];
            modelRangeResidualColors = new Color[ModelPixelCount];
            modelIntensityColors = new Color[ModelPixelCount];
        }

        void DestroyDebugTextures()
        {
            Destroy(ModelRaycastTex);
            Destroy(ModelDetectionMaskTex);
            Destroy(ModelDetectionTex);
            Destroy(ModelRangeTex);
            Destroy(ModelRaycastRangeTex);
            Destroy(ModelRangeResidualTex);
            Destroy(ModelIntensityTex);
            Destroy(modelDebugBackground);
        }

        bool TryGetModelPixelIndex(int row, int col, out int index)
        {
            index = -1;
            if (!IsModelOutputReady()) return false;
            if (row < 0 || row >= ModelImageHeight || col < 0 || col >= ModelImageWidth) return false;
            index = row * ModelImageWidth + col;
            return true;
        }

        void InitModelTextures()
        {
            ModelRaycastTex = CreateModelTexture();
            ModelDetectionMaskTex = CreateModelTexture();
            ModelDetectionTex = CreateModelTexture();
            ModelRangeTex = CreateModelTexture();
            ModelRaycastRangeTex = CreateModelTexture();
            ModelRangeResidualTex = CreateModelTexture();
            ModelIntensityTex = CreateModelTexture();
        }

        static Texture2D CreateModelTexture()
        {
            return new Texture2D(ModelImageWidth, ModelImageHeight, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
        }

        void MaybeLogModelStats()
        {
            if (!ModelLogStats || Time.unscaledTime < modelNextLogTime) return;
            modelNextLogTime = Time.unscaledTime + 1f;
            LogModelStats();
        }

        [ContextMenu("Log Sonar Model Stats")]
        public void LogModelStats()
        {
            Debug.Log(
                $"[{name}] Sonar ML stats: " +
                $"detected={ModelDetectedPointCount}/{ModelPixelCount} " +
                $"({ModelDetectionRule}), " +
                $"raycastHits={ModelRaycastHitCount}, " +
                $"input={ModelInputBuildMs:F2} ms, " +
                $"inference={ModelInferenceMs:F2} ms (avg {ModelInferenceMsAvg:F2} ms), " +
                $"post={ModelPostProcessMs:F2} ms, " +
                $"total={ModelPipelineMs:F2} ms, " +
                $"provider={ModelExecutionProvider}");
        }

        void UpdateModelTextures()
        {
            if (!IsModelOutputReady()) return;

            float maxRange = Mathf.Max(ModelMaxRange, 0.001f);
            float residualRange = Mathf.Max(ModelDebugResidualRange, 0.01f);

            for (int row = 0; row < ModelImageHeight; row++)
            {
                for (int col = 0; col < ModelImageWidth; col++)
                {
                    int i = row * ModelImageWidth + col;
                    int texIndex = (ModelImageHeight - 1 - row) * ModelImageWidth + col;
                    modelRaycastColors[texIndex] = modelRaycastValid[i] ? Color.white : Color.black;
                    modelMaskColors[texIndex] = ModelDetected[i] ? Color.white : Color.black;
                    modelDetectionColors[texIndex] = ColorFromScalar(ModelDetection[i], 0f, 1f);
                    modelRangeColors[texIndex] = ColorFromScalar(ModelRange[i], 0f, maxRange);
                    modelRaycastRangeColors[texIndex] = modelRaycastValid[i]
                        ? ColorFromScalar(modelInputRanges[i], 0f, maxRange) : Color.black;
                    modelRangeResidualColors[texIndex] = ColorFromResidual(ModelRangeResidual[i], residualRange);
                    modelIntensityColors[texIndex] = ColorFromScalar(ModelIntensity[i], 0f, modelIntensityMax);
                }
            }

            ModelRaycastTex.SetPixels(modelRaycastColors);
            ModelDetectionMaskTex.SetPixels(modelMaskColors);
            ModelDetectionTex.SetPixels(modelDetectionColors);
            ModelRangeTex.SetPixels(modelRangeColors);
            ModelRaycastRangeTex.SetPixels(modelRaycastRangeColors);
            ModelRangeResidualTex.SetPixels(modelRangeResidualColors);
            ModelIntensityTex.SetPixels(modelIntensityColors);
            ModelRaycastTex.Apply(false);
            ModelDetectionMaskTex.Apply(false);
            ModelDetectionTex.Apply(false);
            ModelRangeTex.Apply(false);
            ModelRaycastRangeTex.Apply(false);
            ModelRangeResidualTex.Apply(false);
            ModelIntensityTex.Apply(false);
        }

        static Color ColorFromScalar(float value, float min, float max)
        {
            if (!IsFinite(value)) return Color.black;
            float t = max > min ? Mathf.Clamp01((value - min) / (max - min)) : 0f;
            return Color.HSVToRGB(0.66f * (1f - t), 1f, 1f);
        }

        static Color ColorFromResidual(float residual, float scale)
        {
            if (!IsFinite(residual)) return Color.black;
            return Color.Lerp(Color.white, residual < 0f ? Color.blue : Color.red,
                Mathf.Abs(residual) / Mathf.Max(scale, 0.01f));
        }

        void OnDrawGizmosSelected()
        {
            if (!DrawModelDebug || !IsModelOutputReady()) return;

            int stride = Mathf.Max(1, ModelGizmoStride);
            Color previousColor = Gizmos.color;
            for (int row = 0; row < ModelImageHeight; row += stride)
            {
                for (int col = 0; col < ModelImageWidth; col += stride)
                {
                    int i = row * ModelImageWidth + col;
                    if (modelRaycastValid[i])
                    {
                        Color rawColor = ModelRaycastHitColor;
                        rawColor.a = 1f;
                        Gizmos.color = rawColor;
                        Gizmos.DrawCube(modelRaycastPoints[i], Vector3.one * 0.08f);
                    }
                    if (!modelReady || !ModelDetected[i]) continue;
                    Gizmos.color = ColorFromScalar(ModelDetection[i], 0f, 1f);
                    Gizmos.DrawSphere(SonarHits[i].Hit.point, 0.05f);
                }
            }
            Gizmos.color = previousColor;
        }

        void EnsureDebugWindowStyles()
        {
            if (modelDebugWindowStyle != null) return;
            modelDebugBackground = new Texture2D(1, 1);
            modelDebugBackground.SetPixel(0, 0, new Color(0.07f, 0.08f, 0.10f, 1f));
            modelDebugBackground.Apply();
            modelDebugWindowStyle = new GUIStyle(GUI.skin.window) { fontSize = 16 };
            foreach (var state in new[] { modelDebugWindowStyle.normal, modelDebugWindowStyle.hover,
                modelDebugWindowStyle.active, modelDebugWindowStyle.focused, modelDebugWindowStyle.onNormal,
                modelDebugWindowStyle.onHover, modelDebugWindowStyle.onActive, modelDebugWindowStyle.onFocused })
            {
                state.background = modelDebugBackground;
                state.textColor = Color.white;
            }
            modelDebugLabelStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true };
            modelDebugLabelStyle.normal.textColor = Color.white;
            modelDebugHeadingStyle = new GUIStyle(modelDebugLabelStyle) { fontStyle = FontStyle.Bold };
        }

        void OnGUI()
        {
            if (!ShowModelDebugWindow) return;
            EnsureDebugWindowStyles();
            modelDebugWindowRect.width = Mathf.Min(640f, Screen.width);
            modelDebugWindowRect.height = Mathf.Min(720f, Screen.height);
            modelDebugWindowRect.x = Mathf.Clamp(modelDebugWindowRect.x, 0f, Screen.width - modelDebugWindowRect.width);
            modelDebugWindowRect.y = Mathf.Clamp(modelDebugWindowRect.y, 0f, Screen.height - modelDebugWindowRect.height);
            Color previousColor = GUI.color;
            GUI.color = Color.white;
            modelDebugWindowRect = GUI.Window(
                ModelDebugWindowBaseId + GetInstanceID(), modelDebugWindowRect,
                DrawModelDebugWindow, $"Sonar ML Debug ({name})", modelDebugWindowStyle);
            GUI.color = previousColor;
        }

        void DebugLabel(string text, bool heading = false)
        {
            GUILayout.Label(text, heading ? modelDebugHeadingStyle : modelDebugLabelStyle);
        }

        void DrawModelDebugWindow(int windowId)
        {
            GUILayout.BeginArea(new Rect(12f, 28f, modelDebugWindowRect.width - 24f, modelDebugWindowRect.height - 40f));
            GUILayout.BeginHorizontal();
            DebugLabel(modelReady ? "ML inference" : "Physics fallback", true);
            if (GUILayout.Button("Log stats", GUILayout.Width(80f))) LogModelStats();
            if (GUILayout.Button("Close", GUILayout.Width(60f))) ShowModelDebugWindow = false;
            GUILayout.EndHorizontal();

            // The viewport stays on screen; all content, including long errors, can scroll.
            modelDebugScroll = GUILayout.BeginScrollView(modelDebugScroll);
            DebugLabel($"Raycast hits: {ModelRaycastHitCount:N0} / {ModelPixelCount:N0}    Accepted: {ModelDetectedPointCount:N0}");
            DebugLabel(ModelDetectionRule);
            DebugLabel($"Input: {ModelInputBuildMs:F2} ms    Inference: {ModelInferenceMs:F2} ms    EMA (30 samples): {ModelInferenceMsAvg:F2} ms");
            DebugLabel($"Post-process: {ModelPostProcessMs:F2} ms    Pipeline: {ModelPipelineMs:F2} ms (excludes raycast jobs)");
            DebugLabel($"Sensor: {frequency:F1} Hz    Fixed-update budget used: {TimeShareInFixedUpdate:P1}");
            DebugLabel($"Configured provider: {ModelExecutionProvider}    Available: {ModelAvailableProviders}");
            DebugLabel($"Model file: {(ModelFileFound ? "found" : "missing")}    {ModelResolvedPath}");
            if (!string.IsNullOrWhiteSpace(ModelLoadError)) DebugLabel(ModelLoadError);
            if (!modelReady && GUILayout.Button("Retry model load")) RetrySonarModelLoad();

            GUILayout.Space(8f);
            // Preserve the original serialized view indices (0..3).
            ModelDebugView = GUILayout.SelectionGrid(ModelDebugView,
                new[] { "Probability", "Ranges", "Intensity", "All", "Raycasts", "Mask" }, 3);
            GUILayout.BeginHorizontal();
            DebugLabel("Image scale");
            ModelDebugWindowScale = GUILayout.HorizontalSlider(ModelDebugWindowScale, 0.25f, 4f);
            DebugLabel($"{ModelDebugWindowScale:F2}x");
            GUILayout.EndHorizontal();

            modelDebugHoverCol = modelDebugHoverRow = -1;
            float width = Mathf.Min(ModelImageWidth * ModelDebugWindowScale, modelDebugWindowRect.width - 64f);
            float height = width * ModelImageHeight / ModelImageWidth;
            if (ModelDebugView == 3 || ModelDebugView == 4)
                DrawDebugHeatmap(ModelRaycastTex, "Raycast detection — white: hit, black: miss", width, height);
            if (ModelDebugView == 3 || ModelDebugView == 0)
            {
                if (modelReady)
                    DrawDebugHeatmap(ModelDetectionTex, "Raw inference probability — blue: 0, red: 1", width, height);
                else DebugLabel("Raw inference probability unavailable during physics fallback.");
            }
            if (ModelDebugView == 3 || ModelDebugView == 5)
                DrawDebugHeatmap(ModelDetectionMaskTex, modelReady
                    ? "Final mask (raycast AND inference) — white: accepted, black: rejected"
                    : "Physics fallback mask — white: hit, black: miss", width, height);
            if (ModelDebugView == 3 || ModelDebugView == 1)
            {
                DrawRangeComparison();
                DrawDebugHeatmap(ModelRangeTex, modelReady
                    ? $"Corrected range = raw + residual (before masking) — 0 to {ModelMaxRange:F1} m"
                    : $"Physics fallback range — 0 to {ModelMaxRange:F1} m", width, height);
            }
            if (ModelDebugView == 3 || ModelDebugView == 2)
                DrawDebugHeatmap(ModelIntensityTex, $"Intensity before masking — blue: 0, red: {modelIntensityMax:F2}", width, height);

            DebugLabel("Scene gizmos: raw hits are solid cubes (white by default); model detections are spheres colored by probability.");
            DebugLabel($"{ModelImageWidth} × {ModelImageHeight} rays    {ModelFovHDeg:F0}° × {ModelFovVDeg:F0}°    {ModelMaxRange:F0} m range. Drag the title bar to move.");
            GUILayout.EndScrollView();

            // Keep pixel details visible while scrolling through the heatmaps.
            string hover = "Hover a heatmap to inspect a pixel.";
            if (TryGetModelPixelIndex(modelDebugHoverRow, modelDebugHoverCol, out int i))
            {
                string probability = modelReady ? ModelDetection[i].ToString("F3") : "n/a";
                string rawRange = modelRaycastValid[i] ? $"{modelInputRanges[i]:F3} m" : "miss";
                string residual = modelReady ? $"{ModelRangeResidual[i]:+0.000;-0.000;0.000} m" : "n/a";
                hover = $"Pixel [{modelDebugHoverRow}, {modelDebugHoverCol}]    Az: {PixelToYawDeg(modelDebugHoverCol):F1}°    El: {PixelToPitchDeg(modelDebugHoverRow):F1}°\n"
                    + $"Raycast: {modelRaycastValid[i]}    p: {probability}    Accepted: {ModelDetected[i]}\n"
                    + $"Raw range: {rawRange}    Residual: {residual}\n"
                    + $"{(modelReady ? "Corrected range" : "Physics range")}: {ModelRange[i]:F3} m    Intensity: {ModelIntensity[i]:F3}";
            }
            GUILayout.Label(hover, modelDebugLabelStyle, GUILayout.Height(84f));
            GUILayout.EndArea();
            GUI.DragWindow(new Rect(0f, 0f, modelDebugWindowRect.width, 24f));
        }

        void DrawRangeComparison()
        {
            float availableWidth = Mathf.Max(1f, modelDebugWindowRect.width - 64f);
            bool sideBySide = availableWidth >= 2f * ModelImageWidth + 12f;
            float columnWidth = sideBySide ? (availableWidth - 12f) / 2f : availableWidth;
            float width = Mathf.Min(ModelImageWidth * ModelDebugWindowScale, columnWidth);
            float height = width * ModelImageHeight / ModelImageWidth;

            if (sideBySide) GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(columnWidth));
            DrawDebugHeatmap(ModelRaycastRangeTex, "Raw raycast range (m)", width, height);
            DebugLabel($"Blue: 0   Red: {ModelMaxRange:F1} m\nBlack: no raycast hit");
            GUILayout.EndVertical();

            if (sideBySide) GUILayout.Space(12f);
            GUILayout.BeginVertical(GUILayout.Width(columnWidth));
            if (modelReady)
            {
                DrawDebugHeatmap(ModelRangeResidualTex, "Inferred range residual (m)", width, height);
                float scale = Mathf.Max(ModelDebugResidualRange, 0.01f);
                DebugLabel($"Blue: -{scale:F2}   White: 0   Red: +{scale:F2} m\nNegative: closer   Positive: farther");
                DebugLabel("Raw model output before masking; colors saturate at the scale limits.");
            }
            else DebugLabel("Range residual unavailable during physics fallback.");
            GUILayout.EndVertical();
            if (sideBySide) GUILayout.EndHorizontal();
        }

        void DrawDebugHeatmap(Texture2D texture, string label, float width, float height)
        {
            if (texture == null) return;
            DebugLabel(label, true);
            Rect rect = GUILayoutUtility.GetRect(width, height, GUILayout.ExpandWidth(false));
            GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, false);
            if (rect.Contains(Event.current.mousePosition))
            {
                modelDebugHoverCol = Mathf.Clamp((int)((Event.current.mousePosition.x - rect.x) / rect.width * ModelImageWidth), 0, ModelImageWidth - 1);
                // Textures already flip the model rows for Unity's bottom-left texture origin.
                modelDebugHoverRow = Mathf.Clamp((int)((Event.current.mousePosition.y - rect.y) / rect.height * ModelImageHeight), 0, ModelImageHeight - 1);
            }
            GUILayout.Space(6f);
        }

        [ContextMenu("Open Sonar Debug Window")]
        void OpenSonarDebugWindow()
        {
            ShowModelDebugWindow = true;
            DrawModelDebug = true;
        }

    }
}
