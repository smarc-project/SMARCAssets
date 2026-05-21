using System.IO;
using UnityEngine;
using UnityEngine.UIElements;


namespace BagReplay
{
#if UNITY_EDITOR
    using UnityEditor;
    using UnityEditor.UIElements;

    [CustomEditor(typeof(BagReplay))]
    public class BagReplayEditor : Editor
    {
        const string BrowseKey = "Smarc.BagReplay.LastBrowseFolder";

        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            InspectorElement.FillDefaultInspector(root, serializedObject, this);

            CreateBrowseButton(root);
            CreateReplayRangeSlider(root);

            var currentTimeField = new TextField("Current time") { isReadOnly = true, focusable = false };
            currentTimeField.bindingPath = "currentTime";
            root.Add(currentTimeField);

            CreateResetPlaybackButton(root);
            return root;
        }

        private void CreateResetPlaybackButton(VisualElement root)
        {
            var browseBtn = new Button(() =>
                {
                    var bagReplay = (BagReplay)target;
                    bagReplay.RestartReplay();
                })
                { text = "Restart Playback" };

            root.Add(browseBtn);
        }

        private void CreateBrowseButton(VisualElement root)
        {
            var filePathProp = serializedObject.FindProperty("filePath");
            var bagReplay = (BagReplay)target;

            // Text field bound to the string
            var pathField = new TextField("Source Bag Path") { isReadOnly = true, focusable = false };
            pathField.bindingPath = "filePath";
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

                        filePathProp.stringValue = path; // Save absolute path directly
                        serializedObject.ApplyModifiedProperties();
                        bagReplay.Initialize();
                    }
                })
                { text = "Browse…" };

            root.Add(browseBtn);
            root.Bind(serializedObject);
        }

        private void CreateReplayRangeSlider(VisualElement root)
        {
            var bagReplay = (BagReplay)target;

            var limitStartProp = serializedObject.FindProperty("limitStart");
            var limitEndProp = serializedObject.FindProperty("limitEnd");
            var rangeProp = serializedObject.FindProperty("replayRange");
            var startProp = rangeProp.FindPropertyRelative("start");
            var endProp = rangeProp.FindPropertyRelative("end");


            // --- Row: [ Start FloatField ] [  MinMaxSlider  ] [ End FloatField ] ---
            var row = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center
                }
            };

            // Compact start field on the left
            var startField = new FloatField("Start")
            {
                isDelayed = true, // nicer editing (apply on enter/focus loss)
                style = { width = 100 } // fixed width so the slider gets the rest
            };
            startField.labelElement.style.minWidth = 0; // keep label compact
            startField.BindProperty(startProp);

            // The slider in the middle
            var slider = new MinMaxSlider
            {
                style =
                {
                    flexGrow = 1,
                    marginLeft = 2,
                    marginRight = 2
                }
            };

            // Compact end field on the right
            var endField = new FloatField("End")
            {
                isDelayed = true,
                style = { width = 100 }
            };
            endField.labelElement.style.minWidth = 0;
            endField.BindProperty(endProp);

            bool ShouldPlayBackReset(float newStart)
            {
                return Mathf.Abs(bagReplay.replayRange.start - newStart) > 0.0001f;
            }

            // --- Sync logic (limits + values) ---
            void SyncAllFromProps()
            {
                slider.lowLimit = bagReplay.limitStart;
                slider.highLimit = bagReplay.limitEnd;

                float clampedStart = Mathf.Clamp(startProp.floatValue, slider.lowLimit, slider.highLimit);
                float clampedEnd = Mathf.Clamp(endProp.floatValue, clampedStart, slider.highLimit);

                var reset = ShouldPlayBackReset(clampedStart);
                slider.SetValueWithoutNotify(new Vector2(clampedStart, clampedEnd));

                if (!bagReplay.evalMode && (clampedStart != startProp.floatValue || clampedEnd != endProp.floatValue))
                {
                    serializedObject.Update();
                    startProp.floatValue = clampedStart;
                    endProp.floatValue = clampedEnd;
                    serializedObject.ApplyModifiedProperties();
                    if (reset) bagReplay.RestartReplay();
                }

                // Keep fields visually in sync without re-triggering callbacks
                startField.SetValueWithoutNotify(clampedStart);
                endField.SetValueWithoutNotify(clampedEnd);
            }

            SyncAllFromProps();

            // Slider → props + fields
            slider.RegisterValueChangedCallback(evt =>
            {
                if (bagReplay.evalMode) return;

                var reset = ShouldPlayBackReset(evt.newValue.x);
                serializedObject.Update();
                startProp.floatValue = evt.newValue.x;
                endProp.floatValue = evt.newValue.y;
                serializedObject.ApplyModifiedProperties();

                startField.SetValueWithoutNotify(evt.newValue.x);
                endField.SetValueWithoutNotify(evt.newValue.y);

                if (reset) bagReplay.RestartReplay();
            });

            // Fields → slider + props (clamped & ordered)
            void PushFieldsToSlider(ChangeEvent<float> changeEvent)
            {
                if (bagReplay.evalMode) return;
                
                float newStart = Mathf.Clamp(startField.value, slider.lowLimit, slider.highLimit);
                float newEnd = Mathf.Clamp(endField.value, newStart, slider.highLimit);

                slider.SetValueWithoutNotify(new Vector2(newStart, newEnd));

                serializedObject.Update();
                startProp.floatValue = newStart;
                endProp.floatValue = newEnd;
                serializedObject.ApplyModifiedProperties();

                if (changeEvent.target == startField) bagReplay.RestartReplay();
            }

            startField.RegisterValueChangedCallback(changeEvent => PushFieldsToSlider(changeEvent));
            endField.RegisterValueChangedCallback(changeEvent => PushFieldsToSlider(changeEvent));

            // React when limit fields change (edit, undo/redo, multi-object, etc.)
            root.TrackPropertyValue(limitStartProp, _ => SyncAllFromProps());
            root.TrackPropertyValue(limitEndProp, _ => SyncAllFromProps());

            // Build the row
            row.Add(startField);
            row.Add(slider);
            row.Add(endField);

            // Optional label for the group
            var box = new Box();
            box.Add(new Label("Replay Range"));
            box.Add(row);

            root.Add(box);
        }
    }
#endif
}