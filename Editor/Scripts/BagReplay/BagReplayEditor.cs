using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;
using BagReplayComponent = BagReplay.BagReplay;

namespace BagReplay
{
#if UNITY_EDITOR
    using UnityEditor;
    using UnityEditor.UIElements;

    [CustomEditor(typeof(BagReplayComponent))]
    public class BagReplayEditor : Editor
    {
        private const string BrowseKey = "Smarc.BagReplay.LastBrowseFolder";
        private const float DebugRowHeight = 18f;

        private TextField currentTimeField;
        private TextField pathField;
        private ListView topicListView;
        private IMGUIContainer debugTopicSelectionContainer;
        private HelpBox debugHelpBox;
        private IMGUIContainer debugValuesContainer;
        private double nextRefreshTime;

        private BagReplayComponent TargetReplay => (BagReplayComponent)target;

        private void OnEnable()
        {
            EditorApplication.update += EditorTick;
        }

        private void OnDisable()
        {
            EditorApplication.update -= EditorTick;
        }

        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            InspectorElement.FillDefaultInspector(root, serializedObject, this);

            CreateBagSourceSection(root);
            CreateReplayRangeSlider(root);
            CreateCurrentTimeField(root);
            CreateResetPlaybackButton(root);
            CreateTopicsSection(root);
            CreateDebugSection(root);

            RefreshAllViews();
            return root;
        }

        private void CreateBagSourceSection(VisualElement root)
        {
            var container = new Box();
            container.Add(new Label("Bag Source"));

            pathField = new TextField("Source Bag Path")
            {
                isReadOnly = true,
                focusable = false
            };
            container.Add(pathField);

            var buttonRow = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row
                }
            };

            var browseButton = new Button(() =>
            {
                var defaultPath = EditorPrefs.GetString(BrowseKey, Application.dataPath);
                var path = EditorUtility.OpenFilePanel("Select ROSBag", defaultPath, "db3");
                if (string.IsNullOrEmpty(path))
                {
                    return;
                }

                var folder = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(folder))
                {
                    EditorPrefs.SetString(BrowseKey, folder);
                }

                var filePathProperty = serializedObject.FindProperty("filePath");
                filePathProperty.stringValue = path;
                serializedObject.ApplyModifiedProperties();

                TargetReplay.Initialize();
                EditorUtility.SetDirty(TargetReplay);
                serializedObject.Update();
                RefreshAllViews();
            })
            {
                text = "Browse..."
            };
            browseButton.style.marginRight = 6f;

            var reloadButton = new Button(() =>
            {
                TargetReplay.Initialize();
                EditorUtility.SetDirty(TargetReplay);
                serializedObject.Update();
                RefreshAllViews();
            })
            {
                text = "Reload Bag"
            };

            buttonRow.Add(browseButton);
            buttonRow.Add(reloadButton);
            container.Add(buttonRow);
            root.Add(container);
        }

        private void CreateResetPlaybackButton(VisualElement root)
        {
            var resetButton = new Button(() =>
            {
                TargetReplay.RestartReplay();
                EditorUtility.SetDirty(TargetReplay);
                RefreshDebugSection();
                RefreshCurrentTimeField();
            })
            {
                text = "Restart Playback"
            };

            root.Add(resetButton);
        }

        private void CreateCurrentTimeField(VisualElement root)
        {
            currentTimeField = new TextField("Current Time (s)")
            {
                isReadOnly = true,
                focusable = false
            };

            root.Add(currentTimeField);
        }

        private void CreateTopicsSection(VisualElement root)
        {
            var foldout = new Foldout
            {
                text = "Topics",
                value = true
            };

            foldout.Add(CreateTopicHeaderRow());

            topicListView = new ListView
            {
                selectionType = SelectionType.None,
                showBorder = true,
                showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly,
                fixedItemHeight = 28f,
                virtualizationMethod = CollectionVirtualizationMethod.FixedHeight,
                makeItem = CreateTopicListItem,
                bindItem = BindTopicListItem
            };

            topicListView.style.minHeight = 180f;
            foldout.Add(topicListView);
            root.Add(foldout);
        }

        private void CreateDebugSection(VisualElement root)
        {
            var foldout = new Foldout
            {
                text = "Debug Values",
                value = true
            };

            debugTopicSelectionContainer = new IMGUIContainer(DrawDebugTopicSelectionControls);
            debugHelpBox = new HelpBox("Enable a topic with a valid message mapping to inspect its current value.", HelpBoxMessageType.Info);
            debugValuesContainer = new IMGUIContainer(DrawDebugValues);

            foldout.Add(debugTopicSelectionContainer);
            foldout.Add(debugHelpBox);
            foldout.Add(debugValuesContainer);
            root.Add(foldout);
        }

        private VisualElement CreateTopicHeaderRow()
        {
            var header = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    marginBottom = 4
                }
            };

            header.Add(CreateHeaderLabel("Enabled", 70f));
            header.Add(CreateHeaderLabel("Topic", 260f));
            header.Add(CreateHeaderLabel("ROS Type", 220f));
            header.Add(CreateHeaderLabel("Count", 70f));
            header.Add(CreateHeaderLabel("Mapping", 260f));
            header.Add(CreateHeaderLabel("Status", 260f));

            return header;
        }

        private static Label CreateHeaderLabel(string text, float width)
        {
            return new Label(text)
            {
                style =
                {
                    width = width,
                    unityFontStyleAndWeight = FontStyle.Bold
                }
            };
        }

        private VisualElement CreateTopicListItem()
        {
            var controller = new TopicBindingRowController(HandleTopicBindingChanged);
            return controller.Root;
        }

        private void BindTopicListItem(VisualElement element, int index)
        {
            var controller = element.userData as TopicBindingRowController;
            if (controller == null)
            {
                return;
            }

            var topicBindings = TargetReplay.TopicBindings.ToList();
            if (index < 0 || index >= topicBindings.Count)
            {
                controller.Bind(null, new List<MappingOption>());
                return;
            }

            var binding = topicBindings[index];
            controller.Bind(binding, BuildMappingOptions(binding));
        }

        private void HandleTopicBindingChanged()
        {
            TargetReplay.RefreshTopicConfiguration(restartPlayback: !TargetReplay.evalMode);
            EditorUtility.SetDirty(TargetReplay);
            serializedObject.Update();
            RefreshAllViews();
        }

        private void CreateReplayRangeSlider(VisualElement root)
        {
            var bagReplay = TargetReplay;
            var limitStartProperty = serializedObject.FindProperty("limitStart");
            var limitEndProperty = serializedObject.FindProperty("limitEnd");
            var rangeProperty = serializedObject.FindProperty("replayRange");
            var startProperty = rangeProperty.FindPropertyRelative("start");
            var endProperty = rangeProperty.FindPropertyRelative("end");

            var row = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center
                }
            };

            var startField = new FloatField("Start")
            {
                isDelayed = true,
                style = { width = 100 }
            };
            startField.labelElement.style.minWidth = 0;

            var slider = new MinMaxSlider
            {
                style =
                {
                    flexGrow = 1,
                    marginLeft = 2,
                    marginRight = 2
                }
            };

            var endField = new FloatField("End")
            {
                isDelayed = true,
                style = { width = 100 }
            };
            endField.labelElement.style.minWidth = 0;

            bool ShouldRestart(float newStart)
            {
                return Mathf.Abs(bagReplay.replayRange.start - newStart) > 0.0001f;
            }

            void SyncAllFromProperties()
            {
                slider.lowLimit = bagReplay.limitStart;
                slider.highLimit = bagReplay.limitEnd;

                var clampedStart = Mathf.Clamp(startProperty.floatValue, slider.lowLimit, slider.highLimit);
                var clampedEnd = Mathf.Clamp(endProperty.floatValue, clampedStart, slider.highLimit);

                slider.SetValueWithoutNotify(new Vector2(clampedStart, clampedEnd));
                startField.SetValueWithoutNotify(clampedStart);
                endField.SetValueWithoutNotify(clampedEnd);
            }

            SyncAllFromProperties();

            slider.RegisterValueChangedCallback(evt =>
            {
                if (bagReplay.evalMode)
                {
                    return;
                }

                var shouldRestart = ShouldRestart(evt.newValue.x);
                serializedObject.Update();
                startProperty.floatValue = evt.newValue.x;
                endProperty.floatValue = evt.newValue.y;
                serializedObject.ApplyModifiedProperties();

                startField.SetValueWithoutNotify(evt.newValue.x);
                endField.SetValueWithoutNotify(evt.newValue.y);

                if (shouldRestart)
                {
                    bagReplay.RestartReplay();
                    RefreshCurrentTimeField();
                }
            });

            void PushFieldsToSlider(ChangeEvent<float> changeEvent)
            {
                if (bagReplay.evalMode)
                {
                    return;
                }

                var newStart = Mathf.Clamp(startField.value, slider.lowLimit, slider.highLimit);
                var newEnd = Mathf.Clamp(endField.value, newStart, slider.highLimit);

                slider.SetValueWithoutNotify(new Vector2(newStart, newEnd));

                serializedObject.Update();
                startProperty.floatValue = newStart;
                endProperty.floatValue = newEnd;
                serializedObject.ApplyModifiedProperties();

                if (changeEvent.target == startField)
                {
                    bagReplay.RestartReplay();
                    RefreshCurrentTimeField();
                }
            }

            startField.RegisterValueChangedCallback(PushFieldsToSlider);
            endField.RegisterValueChangedCallback(PushFieldsToSlider);

            root.TrackPropertyValue(limitStartProperty, _ => SyncAllFromProperties());
            root.TrackPropertyValue(limitEndProperty, _ => SyncAllFromProperties());

            row.Add(startField);
            row.Add(slider);
            row.Add(endField);

            var box = new Box();
            box.Add(new Label("Replay Range"));
            box.Add(row);
            root.Add(box);
        }

        private void RefreshAllViews()
        {
            RefreshPathField();
            RefreshTopicList();
            RefreshDebugSection();
            RefreshCurrentTimeField();
        }

        private void RefreshPathField()
        {
            pathField?.SetValueWithoutNotify(TargetReplay.FilePath ?? string.Empty);
        }

        private void RefreshTopicList()
        {
            if (topicListView == null)
            {
                return;
            }

            topicListView.itemsSource = TargetReplay.TopicBindings.ToList();
            topicListView.Rebuild();
        }

        private void RefreshDebugSection()
        {
            if (debugTopicSelectionContainer == null || debugHelpBox == null)
            {
                return;
            }

            var debugTopicNames = TargetReplay.GetAvailableDebugTopicNames().ToList();

            var hasTopics = debugTopicNames.Count > 0;
            debugTopicSelectionContainer.style.display = hasTopics ? DisplayStyle.Flex : DisplayStyle.None;
            debugHelpBox.style.display = hasTopics ? DisplayStyle.None : DisplayStyle.Flex;
            debugValuesContainer.style.display = hasTopics ? DisplayStyle.Flex : DisplayStyle.None;

            if (!hasTopics)
            {
                if (TargetReplay.SelectedDebugTopicNames.Count > 0)
                {
                    TargetReplay.SetSelectedDebugTopics(Array.Empty<string>());
                    EditorUtility.SetDirty(TargetReplay);
                }

                debugTopicSelectionContainer.MarkDirtyRepaint();
                debugValuesContainer.MarkDirtyRepaint();
                return;
            }

            var normalizedSelection = TargetReplay.SelectedDebugTopicNames
                .Where(debugTopicNames.Contains)
                .ToList();

            if (!TargetReplay.SelectedDebugTopicNames.SequenceEqual(normalizedSelection, StringComparer.Ordinal))
            {
                TargetReplay.SetSelectedDebugTopics(normalizedSelection);
                EditorUtility.SetDirty(TargetReplay);
            }

            debugTopicSelectionContainer.MarkDirtyRepaint();
            debugValuesContainer.MarkDirtyRepaint();
        }

        private void RefreshCurrentTimeField()
        {
            currentTimeField?.SetValueWithoutNotify(TargetReplay.currentTime.ToString("0.###"));
        }

        private void EditorTick()
        {
            if (EditorApplication.timeSinceStartup < nextRefreshTime)
            {
                return;
            }

            nextRefreshTime = EditorApplication.timeSinceStartup + 0.1d;
            RefreshCurrentTimeField();
            debugValuesContainer?.MarkDirtyRepaint();
        }

        private void DrawDebugTopicSelectionControls()
        {
            var availableTopicNames = TargetReplay.GetAvailableDebugTopicNames().ToList();
            if (availableTopicNames.Count == 0)
            {
                return;
            }

            var selectedTopics = TargetReplay.SelectedDebugTopicNames.ToList();
            List<string> pendingSelection = null;

            for (var index = 0; index < selectedTopics.Count; index++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    var currentTopic = selectedTopics[index];
                    EditorGUILayout.PrefixLabel($"View {index + 1}");

                    var dropdownRect = GUILayoutUtility.GetRect(
                        new GUIContent(currentTopic),
                        EditorStyles.popup,
                        GUILayout.ExpandWidth(true));

                    if (EditorGUI.DropdownButton(dropdownRect, new GUIContent(currentTopic), FocusType.Passive, EditorStyles.popup))
                    {
                        ShowDebugTopicMenu(index, currentTopic, availableTopicNames, dropdownRect);
                    }

                    if (GUILayout.Button("Remove", GUILayout.Width(70f)))
                    {
                        pendingSelection = selectedTopics.ToList();
                        pendingSelection.RemoveAt(index);
                    }
                }

                if (pendingSelection != null)
                {
                    ApplyDebugTopicSelectionChange(pendingSelection);
                    return;
                }
            }

            if (GUILayout.Button("Add Topic View"))
            {
                var nextSelection = selectedTopics.ToList();
                var nextTopic = availableTopicNames
                    .Except(nextSelection, StringComparer.Ordinal)
                    .FirstOrDefault()
                    ?? availableTopicNames[0];

                nextSelection.Add(nextTopic);
                ApplyDebugTopicSelectionChange(nextSelection);
            }
        }

        private void ShowDebugTopicMenu(
            int selectedIndex,
            string currentTopic,
            IReadOnlyList<string> availableTopicNames,
            Rect dropdownRect)
        {
            var menu = new GenericMenu();
            foreach (var topicName in availableTopicNames)
            {
                var capturedTopicName = topicName;
                menu.AddItem(
                    new GUIContent(BuildDebugTopicMenuLabel(capturedTopicName)),
                    string.Equals(capturedTopicName, currentTopic, StringComparison.Ordinal),
                    () => SelectDebugTopic(selectedIndex, capturedTopicName));
            }

            var menuRect = new Rect(
                dropdownRect.x,
                dropdownRect.y,
                Mathf.Max(dropdownRect.width, 320f),
                dropdownRect.height);

            menu.DropDown(menuRect);
        }

        private void DrawDebugValues()
        {
            var selectedTopics = TargetReplay.SelectedDebugTopicNames.ToList();
            if (selectedTopics.Count == 0)
            {
                EditorGUILayout.HelpBox("Add one or more topic views above to inspect their live values.", MessageType.Info);
                return;
            }

            foreach (var selectedTopic in selectedTopics)
            {
                if (!TargetReplay.TryGetCurrentTopicValue(selectedTopic, out var playbackValue) ||
                    playbackValue?.Message == null)
                {
                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        EditorGUILayout.LabelField(selectedTopic, EditorStyles.boldLabel);
                        EditorGUILayout.HelpBox("No live value is available for this topic yet.", MessageType.Info);
                    }

                    EditorGUILayout.Space(6);
                    continue;
                }

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField(playbackValue.TopicName, EditorStyles.boldLabel);
                    DrawDebugHeaderRow("Field", "Value");
                    DrawDebugRow("Topic", playbackValue.TopicName);
                    DrawDebugRow("ROS Type", playbackValue.RosTypeName);
                    DrawDebugRow("Mapped As", playbackValue.ResolvedRosMessageName);
                    DrawDebugRow("Timestamp", playbackValue.TimestampNanos.ToString());

                    EditorGUILayout.Space(6);
                    DrawDebugHeaderRow("Message Field", "Current Value");
                    foreach (var row in DebugValueFormatter.Flatten(playbackValue.Message))
                    {
                        DrawDebugRow(row.Label, row.Value);
                    }
                }

                EditorGUILayout.Space(6);
            }
        }

        private void UpdateSelectedDebugTopics(IEnumerable<string> topicNames)
        {
            TargetReplay.SetSelectedDebugTopics(topicNames);
            EditorUtility.SetDirty(TargetReplay);
            RefreshDebugSection();
        }

        private void ApplyDebugTopicSelectionChange(IEnumerable<string> topicNames)
        {
            UpdateSelectedDebugTopics(topicNames);
            GUIUtility.ExitGUI();
        }

        private static string BuildDebugTopicMenuLabel(string topicName)
        {
            if (string.IsNullOrWhiteSpace(topicName))
            {
                return "(Unnamed Topic)";
            }

            var segments = topicName
                .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(segment => !string.IsNullOrWhiteSpace(segment))
                .ToArray();

            if (segments.Length == 0)
            {
                return topicName;
            }

            return string.Join(" > ", segments);
        }

        private void SelectDebugTopic(int selectedIndex, string topicName)
        {
            var selectedTopics = TargetReplay.SelectedDebugTopicNames.ToList();
            if (selectedIndex < 0 || selectedIndex >= selectedTopics.Count)
            {
                return;
            }

            selectedTopics[selectedIndex] = topicName ?? string.Empty;
            UpdateSelectedDebugTopics(selectedTopics);
        }

        private static void DrawDebugHeaderRow(string leftText, string rightText)
        {
            var rect = EditorGUILayout.GetControlRect(false, DebugRowHeight);
            var left = new Rect(rect.x, rect.y, 160f, rect.height);
            var right = new Rect(rect.x + 160f, rect.y, rect.width - 160f, rect.height);

            EditorGUI.LabelField(left, leftText, EditorStyles.boldLabel);
            EditorGUI.LabelField(right, rightText, EditorStyles.boldLabel);
        }

        private static void DrawDebugRow(string leftText, string rightText)
        {
            var rect = EditorGUILayout.GetControlRect(false, DebugRowHeight);
            var left = new Rect(rect.x, rect.y, 160f, rect.height);
            var right = new Rect(rect.x + 160f, rect.y, rect.width - 160f, rect.height);

            EditorGUI.LabelField(left, leftText);
            EditorGUI.SelectableLabel(right, rightText, EditorStyles.label);
        }

        private static List<MappingOption> BuildMappingOptions(BagTopicBinding binding)
        {
            var options = new List<MappingOption>
            {
                new MappingOption(BagTopicMappingMode.Auto, string.Empty, BuildAutoOptionLabel(binding)),
                new MappingOption(BagTopicMappingMode.None, string.Empty, "None")
            };

            var manualOptions = RosMessageCatalog.Entries
                .OrderBy(entry => !string.Equals(entry.RosMessageName, binding.RosTypeName, StringComparison.Ordinal))
                .ThenBy(entry => entry.RosMessageName, StringComparer.Ordinal)
                .Select(entry => new MappingOption(
                    BagTopicMappingMode.Override,
                    entry.RosMessageName,
                    $"Override: {entry.DisplayName}"));

            options.AddRange(manualOptions);
            return options;
        }

        private static string BuildAutoOptionLabel(BagTopicBinding binding)
        {
            if (binding == null)
            {
                return "Auto";
            }

            if (!string.IsNullOrWhiteSpace(binding.ResolvedRosMessageName))
            {
                return $"Auto: {FormatResolvedMappingLabel(binding.ResolvedRosMessageName, binding.ResolvedClrTypeName)}";
            }

            if (RosMessageCatalog.TryResolveRosMessageName(binding.RosTypeName, out _, out var descriptor))
            {
                return $"Auto: {descriptor.DisplayName}";
            }

            return $"Auto ({binding.RosTypeName} - unavailable)";
        }

        private static string FormatResolvedMappingLabel(string rosMessageName, string clrTypeName)
        {
            if (string.IsNullOrWhiteSpace(rosMessageName))
            {
                return "Unavailable";
            }

            var shortClrTypeName = GetShortTypeName(clrTypeName);
            return string.IsNullOrWhiteSpace(shortClrTypeName)
                ? rosMessageName
                : $"{rosMessageName} ({shortClrTypeName})";
        }

        private static string GetShortTypeName(string clrTypeName)
        {
            if (string.IsNullOrWhiteSpace(clrTypeName))
            {
                return string.Empty;
            }

            var lastDotIndex = clrTypeName.LastIndexOf('.');
            var shortName = lastDotIndex >= 0 && lastDotIndex < clrTypeName.Length - 1
                ? clrTypeName.Substring(lastDotIndex + 1)
                : clrTypeName;

            var nestedTypeIndex = shortName.LastIndexOf('+');
            return nestedTypeIndex >= 0 && nestedTypeIndex < shortName.Length - 1
                ? shortName.Substring(nestedTypeIndex + 1)
                : shortName;
        }

        private sealed class TopicBindingRowController
        {
            private readonly Action onChanged;
            private readonly Toggle enabledToggle;
            private readonly Label topicLabel;
            private readonly Label rosTypeLabel;
            private readonly Label countLabel;
            private readonly PopupField<string> mappingPopup;
            private readonly Label statusLabel;
            private List<MappingOption> mappingOptions = new List<MappingOption>();

            public VisualElement Root { get; }
            public BagTopicBinding BoundBinding { get; private set; }

            public TopicBindingRowController(Action onChanged)
            {
                this.onChanged = onChanged;

                Root = new VisualElement
                {
                    style =
                    {
                        flexDirection = FlexDirection.Row,
                        alignItems = Align.Center,
                        height = 26f
                    }
                };
                Root.userData = this;

                enabledToggle = new Toggle
                {
                    style = { width = 70f }
                };
                enabledToggle.RegisterValueChangedCallback(evt =>
                {
                    if (BoundBinding == null || BoundBinding.Enabled == evt.newValue)
                    {
                        return;
                    }

                    BoundBinding.Enabled = evt.newValue;
                    onChanged.Invoke();
                });

                topicLabel = new Label { style = { width = 260f } };
                rosTypeLabel = new Label { style = { width = 220f } };
                countLabel = new Label { style = { width = 70f } };
                mappingPopup = new PopupField<string>
                {
                    style = { width = 260f }
                };
                mappingPopup.RegisterValueChangedCallback(evt =>
                {
                    if (BoundBinding == null)
                    {
                        return;
                    }

                    var option = mappingOptions.FirstOrDefault(x => x.Label == evt.newValue);
                    if (string.IsNullOrWhiteSpace(option.Label))
                    {
                        return;
                    }

                    BoundBinding.MappingMode = option.Mode;
                    BoundBinding.OverrideRosMessageName = option.OverrideRosMessageName;
                    onChanged.Invoke();
                });

                statusLabel = new Label
                {
                    style =
                    {
                        width = 260f,
                        unityTextAlign = TextAnchor.MiddleLeft
                    }
                };

                Root.Add(enabledToggle);
                Root.Add(topicLabel);
                Root.Add(rosTypeLabel);
                Root.Add(countLabel);
                Root.Add(mappingPopup);
                Root.Add(statusLabel);
            }

            public void Bind(BagTopicBinding binding, List<MappingOption> newMappingOptions)
            {
                BoundBinding = binding;
                mappingOptions = newMappingOptions ?? new List<MappingOption>();

                if (binding == null)
                {
                    enabledToggle.SetValueWithoutNotify(false);
                    topicLabel.text = string.Empty;
                    rosTypeLabel.text = string.Empty;
                    countLabel.text = string.Empty;
                    mappingPopup.choices = new List<string> { string.Empty };
                    mappingPopup.SetValueWithoutNotify(string.Empty);
                    statusLabel.text = string.Empty;
                    return;
                }

                enabledToggle.SetValueWithoutNotify(binding.Enabled);
                topicLabel.text = binding.TopicName;
                rosTypeLabel.text = binding.RosTypeName;
                countLabel.text = binding.MessageCount.ToString();

                var labels = mappingOptions.Select(x => x.Label).ToList();
                if (labels.Count == 0)
                {
                    labels.Add(string.Empty);
                }

                mappingPopup.choices = labels;
                mappingPopup.SetValueWithoutNotify(GetSelectedOptionLabel(binding, mappingOptions));

                statusLabel.text = BuildStatus(binding);
                statusLabel.style.color = string.IsNullOrWhiteSpace(binding.ErrorMessage)
                    ? new StyleColor(new Color(0.35f, 0.65f, 0.35f))
                    : new StyleColor(new Color(0.8f, 0.3f, 0.3f));
            }

            private static string GetSelectedOptionLabel(BagTopicBinding binding, IEnumerable<MappingOption> options)
            {
                foreach (var option in options)
                {
                    if (binding.MappingMode != option.Mode)
                    {
                        continue;
                    }

                    if (binding.MappingMode != BagTopicMappingMode.Override ||
                        string.Equals(binding.OverrideRosMessageName, option.OverrideRosMessageName, StringComparison.Ordinal))
                    {
                        return option.Label;
                    }
                }

                return options.FirstOrDefault().Label ?? string.Empty;
            }

            private static string BuildStatus(BagTopicBinding binding)
            {
                if (!binding.Enabled)
                {
                    return "Disabled";
                }

                if (!string.IsNullOrWhiteSpace(binding.ErrorMessage))
                {
                    return binding.ErrorMessage;
                }

                if (binding.MappingMode == BagTopicMappingMode.None)
                {
                    return "Mapping disabled";
                }

                if (!string.IsNullOrWhiteSpace(binding.ResolvedRosMessageName))
                {
                    return $"Mapped: {binding.ResolvedRosMessageName} ({binding.LoadedMessageCount} msgs)";
                }

                return "Awaiting mapping";
            }
        }

        private readonly struct MappingOption
        {
            public BagTopicMappingMode Mode { get; }
            public string OverrideRosMessageName { get; }
            public string Label { get; }

            public MappingOption(BagTopicMappingMode mode, string overrideRosMessageName, string label)
            {
                Mode = mode;
                OverrideRosMessageName = overrideRosMessageName ?? string.Empty;
                Label = label ?? string.Empty;
            }
        }

        private readonly struct DebugValueRow
        {
            public string Label { get; }
            public string Value { get; }

            public DebugValueRow(string label, string value)
            {
                Label = label;
                Value = value;
            }
        }

        private static class DebugValueFormatter
        {
            private const int MaxDepth = 4;
            private const int MaxRows = 80;
            private const int MaxCollectionPreview = 6;

            public static IReadOnlyList<DebugValueRow> Flatten(object value)
            {
                var rows = new List<DebugValueRow>();
                AppendRows(rows, string.Empty, value, 0);
                return rows;
            }

            private static void AppendRows(List<DebugValueRow> rows, string path, object value, int depth)
            {
                if (rows.Count >= MaxRows)
                {
                    return;
                }

                if (value == null)
                {
                    rows.Add(new DebugValueRow(path, "null"));
                    return;
                }

                if (depth > MaxDepth)
                {
                    rows.Add(new DebugValueRow(path, "..."));
                    return;
                }

                var type = value.GetType();
                if (IsScalar(type))
                {
                    rows.Add(new DebugValueRow(path, FormatScalar(value)));
                    return;
                }

                if (value is IEnumerable enumerable && !(value is string))
                {
                    AppendEnumerableRows(rows, path, enumerable, depth);
                    return;
                }

                var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public)
                    .OrderBy(field => field.Name, StringComparer.Ordinal)
                    .ToArray();

                if (fields.Length == 0)
                {
                    rows.Add(new DebugValueRow(path, value.ToString()));
                    return;
                }

                foreach (var field in fields)
                {
                    AppendRows(rows, AppendPath(path, field.Name), field.GetValue(value), depth + 1);
                }
            }

            private static void AppendEnumerableRows(List<DebugValueRow> rows, string path, IEnumerable enumerable, int depth)
            {
                var values = new List<object>();
                foreach (var item in enumerable)
                {
                    values.Add(item);
                    if (values.Count >= MaxCollectionPreview)
                    {
                        break;
                    }
                }

                if (values.Count == 0)
                {
                    rows.Add(new DebugValueRow(path, "[]"));
                    return;
                }

                if (values.All(item => item == null || IsScalar(item.GetType())))
                {
                    var preview = string.Join(", ", values.Select(FormatScalar));
                    rows.Add(new DebugValueRow(path, $"[{preview}]"));
                    return;
                }

                rows.Add(new DebugValueRow(path, $"[{values.Count}+ items]"));
                for (var index = 0; index < values.Count && rows.Count < MaxRows; index++)
                {
                    AppendRows(rows, $"{path}[{index}]", values[index], depth + 1);
                }
            }

            private static bool IsScalar(Type type)
            {
                return type.IsPrimitive
                       || type.IsEnum
                       || type == typeof(decimal)
                       || type == typeof(string)
                       || type == typeof(Guid);
            }

            private static string FormatScalar(object value)
            {
                return value switch
                {
                    null => "null",
                    float floatValue => floatValue.ToString("0.####"),
                    double doubleValue => doubleValue.ToString("0.####"),
                    Vector2 vector2 => $"({vector2.x:0.####}, {vector2.y:0.####})",
                    Vector3 vector3 => $"({vector3.x:0.####}, {vector3.y:0.####}, {vector3.z:0.####})",
                    Quaternion quaternion => $"({quaternion.x:0.####}, {quaternion.y:0.####}, {quaternion.z:0.####}, {quaternion.w:0.####})",
                    _ => value.ToString()
                };
            }

            private static string AppendPath(string prefix, string name)
            {
                return string.IsNullOrWhiteSpace(prefix) ? name : $"{prefix}.{name}";
            }
        }
    }
#endif
}
