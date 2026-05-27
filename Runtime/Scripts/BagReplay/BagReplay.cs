using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace BagReplay
{
    [Serializable]
    public struct FloatRange
    {
        public float start;
        public float end;

        public FloatRange(float start, float end)
        {
            this.start = start;
            this.end = end;
        }
    }

    public class BagReplay : MonoBehaviour
    {
        [SerializeField] [HideInInspector] private string filePath = string.Empty;
        [SerializeField] [HideInInspector] private List<BagTopicBinding> topicBindings = new List<BagTopicBinding>();
        [SerializeField] [HideInInspector] private List<string> selectedDebugTopicNames = new List<string>();
        [SerializeField] [HideInInspector] private string selectedDebugTopicName = string.Empty;

        [HideInInspector] public float limitStart;
        [HideInInspector] public float limitEnd;
        [HideInInspector] public FloatRange replayRange = new FloatRange(0f, 0f);

        public static event Action<BagReplay> OnReplayRestart;
        public static event Action<BagReplay> OnReplayDone;
        public static event Action<BagReplay> OnTopicBindingsChanged;

        [HideInInspector] public double currentTime;
        [HideInInspector] public bool isPlaying;

        public bool stopTimeAtEnd;

        public BagReader BagReader { get; private set; }
        public BagTopicSnapshot CurrentTopicSnapshot { get; private set; } = BagTopicSnapshot.Empty;
        public BagTopicSnapshot NextTopicSnapshot { get; private set; } = BagTopicSnapshot.Empty;
        public BagTopicSnapshot PreviousTopicSnapshot { get; private set; } = BagTopicSnapshot.Empty;
        public IReadOnlyList<BagTopicBinding> TopicBindings => topicBindings;
        public IReadOnlyList<BagTopicInventoryEntry> TopicInventory => BagReader?.TopicInventory ?? Array.Empty<BagTopicInventoryEntry>();
        public IReadOnlyList<string> SelectedDebugTopicNames => selectedDebugTopicNames;
        public string FilePath => filePath;

        public string SelectedDebugTopicName
        {
            get => selectedDebugTopicName;
            set
            {
                selectedDebugTopicName = value ?? string.Empty;
                SetSelectedDebugTopics(string.IsNullOrWhiteSpace(selectedDebugTopicName)
                    ? Array.Empty<string>()
                    : new[] { selectedDebugTopicName });
            }
        }

        public bool HasLoadedBag => BagReader != null;

        private void Awake()
        {
            Initialize();
        }

        public void Initialize()
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                ResetState(clearBindings: true);
                return;
            }

            if (!File.Exists(filePath))
            {
                Debug.LogWarning($"BagReplay could not find bag file at '{filePath}'.");
                ResetState(clearBindings: true);
                return;
            }

            var previousBindings = topicBindings ?? new List<BagTopicBinding>();
            BagReader = new BagReader(filePath);
            topicBindings = MergeBindings(BagReader.TopicInventory, previousBindings);

            limitStart = 0f;
            limitEnd = BagReader.EndNanos > BagReader.StartNanos
                ? (float)((BagReader.EndNanos - BagReader.StartNanos) / 1_000_000_000d)
                : 0f;

            NormalizeReplayRange();
            RefreshTopicConfiguration(restartPlayback: false);
            RestartReplayAt((float)currentTime);
        }

        public void RestartReplay()
        {
            if (BagReader == null)
            {
                ResetState(clearBindings: false);
                return;
            }

            RestartReplayAt(replayRange.start);
        }

        public void RestartReplayAt(float timeSeconds)
        {
            if (BagReader == null)
            {
                currentTime = ClampReplayTime(timeSeconds);
                ResetSnapshots();
                return;
            }

            currentTime = ClampReplayTime(timeSeconds);
            PreviousTopicSnapshot = BagTopicSnapshot.Empty;
            CurrentTopicSnapshot = BagTopicSnapshot.Empty;
            NextTopicSnapshot = BagTopicSnapshot.Empty;
            UpdateTopicSnapshots();

            isPlaying = true;
            if (Application.isPlaying)
            {
                OnReplayRestart?.Invoke(this);
            }
        }

        public void Seek(float timeSeconds)
        {
            RestartReplayAt(timeSeconds);
        }

        public void ClampCurrentTimeToReplayRange()
        {
            var clampedTime = ClampReplayTime((float)currentTime);
            if (Mathf.Abs((float)currentTime - clampedTime) > 0.0001f)
            {
                RestartReplayAt(clampedTime);
                return;
            }

            currentTime = clampedTime;
            UpdateTopicSnapshots();
        }

        public float ClampReplayTime(float timeSeconds)
        {
            if (float.IsNaN(timeSeconds) || float.IsInfinity(timeSeconds))
            {
                timeSeconds = replayRange.start;
            }

            var min = replayRange.start;
            var max = replayRange.end;
            if (max <= min)
            {
                min = limitStart;
                max = limitEnd;
            }

            return max > min ? Mathf.Clamp(timeSeconds, min, max) : min;
        }

        public void RefreshTopicConfiguration(bool restartPlayback = true)
        {
            if (BagReader == null)
            {
                ResetSnapshots();
                NotifyTopicBindingsChanged();
                return;
            }

            BagReader.LoadBindings(topicBindings);
            EnsureDebugTopicSelection();
            NotifyTopicBindingsChanged();

            if (restartPlayback)
            {
                RestartReplayAt((float)currentTime);
            }
            else
            {
                UpdateTopicSnapshots();
            }
        }

        public IEnumerable<BagTopicBinding> GetEnabledBindings()
        {
            return topicBindings.Where(binding => binding.Enabled);
        }

        public IEnumerable<BagTopicBinding> GetDebuggableBindings()
        {
            return topicBindings.Where(binding => binding.Enabled && binding.HasResolvedMapping);
        }

        public IEnumerable<string> GetAvailableDebugTopicNames()
        {
            return GetDebuggableBindings().Select(binding => binding.TopicName);
        }

        public void SetSelectedDebugTopics(IEnumerable<string> topicNames)
        {
            selectedDebugTopicNames = NormalizeSelectedDebugTopics(topicNames);
            selectedDebugTopicName = selectedDebugTopicNames.FirstOrDefault() ?? string.Empty;
        }

        public bool TryGetBinding(string topicName, string rosTypeName, out BagTopicBinding binding)
        {
            binding = topicBindings.FirstOrDefault(x =>
                string.Equals(x.TopicName, topicName, StringComparison.Ordinal) &&
                RosMessageCatalog.AreEquivalentRosMessageNames(x.RosTypeName, rosTypeName));

            return binding != null;
        }

        public bool TryGetCurrentTopicValue(string topicName, out BagTopicPlaybackValue playbackValue)
        {
            return CurrentTopicSnapshot.TryGetValue(topicName, out playbackValue);
        }

        public bool TryGetNextTopicValue(string topicName, out BagTopicPlaybackValue playbackValue)
        {
            return NextTopicSnapshot.TryGetValue(topicName, out playbackValue);
        }

        public bool TryGetPreviousTopicValue(string topicName, out BagTopicPlaybackValue playbackValue)
        {
            return PreviousTopicSnapshot.TryGetValue(topicName, out playbackValue);
        }

        private void FixedUpdate()
        {
            if (!isPlaying || BagReader == null)
            {
                return;
            }

            var nextTime = currentTime + Time.fixedDeltaTime;
            if (replayRange.end > 0f && nextTime > replayRange.end)
            {
                currentTime = ClampReplayTime(replayRange.end);
                UpdateTopicSnapshots();

                if (Application.isPlaying && isPlaying)
                {
                    isPlaying = false;
                    if (stopTimeAtEnd)
                    {
                        Time.timeScale = 0f;
                    }

                    OnReplayDone?.Invoke(this);
                }

                return;
            }

            currentTime = nextTime;
            UpdateTopicSnapshots();
        }

        public void RefreshSnapshotsAtCurrentTime()
        {
            if (BagReader == null)
            {
                ResetSnapshots();
                return;
            }

            currentTime = ClampReplayTime((float)currentTime);
            UpdateTopicSnapshots();
        }

        private void UpdateTopicSnapshots()
        {
            if (BagReader == null)
            {
                ResetSnapshots();
                return;
            }

            var queryTime = BagReader.StartNanos + currentTime * 1_000_000_000d;
            if (BagReader.EndNanos > BagReader.StartNanos)
            {
                queryTime = Math.Min(Math.Max(queryTime, BagReader.StartNanos), BagReader.EndNanos);
            }

            PreviousTopicSnapshot = CurrentTopicSnapshot;
            CurrentTopicSnapshot = BagReader.ReadSnapshot(queryTime);
            NextTopicSnapshot = BagReader.ReadSnapshot(queryTime + Time.fixedDeltaTime * 1_000_000_000d);
            EnsureDebugTopicSelection();
        }

        private void ResetState(bool clearBindings)
        {
            BagReader = null;

            limitStart = 0f;
            limitEnd = 0f;
            replayRange = new FloatRange(0f, 0f);
            currentTime = 0d;
            isPlaying = false;

            if (clearBindings)
            {
                topicBindings = new List<BagTopicBinding>();
            }

            ResetSnapshots();
            NotifyTopicBindingsChanged();
        }

        private void ResetSnapshots()
        {
            CurrentTopicSnapshot = BagTopicSnapshot.Empty;
            NextTopicSnapshot = BagTopicSnapshot.Empty;
            PreviousTopicSnapshot = BagTopicSnapshot.Empty;
            EnsureDebugTopicSelection();
        }

        private void NormalizeReplayRange()
        {
            if (limitEnd <= 0f)
            {
                replayRange = new FloatRange(0f, 0f);
                return;
            }

            var start = Mathf.Clamp(replayRange.start, limitStart, limitEnd);
            var defaultEnd = replayRange.end <= 0f ? limitEnd : replayRange.end;
            var end = Mathf.Clamp(defaultEnd, start, limitEnd);

            if (Mathf.Approximately(end, start))
            {
                end = limitEnd;
            }

            replayRange = new FloatRange(start, end);
        }

        private void EnsureDebugTopicSelection()
        {
            var availableTopics = new HashSet<string>(GetAvailableDebugTopicNames(), StringComparer.Ordinal);
            if (availableTopics.Count == 0)
            {
                selectedDebugTopicNames.Clear();
                selectedDebugTopicName = string.Empty;
                return;
            }

            var normalizedSelection = NormalizeSelectedDebugTopics(selectedDebugTopicNames
                .Where(topicName => availableTopics.Contains(topicName)));

            if (normalizedSelection.Count == 0 && !string.IsNullOrWhiteSpace(selectedDebugTopicName) &&
                availableTopics.Contains(selectedDebugTopicName))
            {
                normalizedSelection.Add(selectedDebugTopicName);
            }

            selectedDebugTopicNames = normalizedSelection;
            selectedDebugTopicName = selectedDebugTopicNames.FirstOrDefault() ?? string.Empty;
        }

        private static List<string> NormalizeSelectedDebugTopics(IEnumerable<string> topicNames)
        {
            var normalizedTopics = new List<string>();
            if (topicNames == null)
            {
                return normalizedTopics;
            }

            foreach (var topicName in topicNames)
            {
                if (string.IsNullOrWhiteSpace(topicName))
                {
                    continue;
                }

                normalizedTopics.Add(topicName);
            }

            return normalizedTopics;
        }

        private static List<BagTopicBinding> MergeBindings(
            IReadOnlyList<BagTopicInventoryEntry> inventoryEntries,
            IReadOnlyList<BagTopicBinding> previousBindings)
        {
            var previousByKey = new Dictionary<string, BagTopicBinding>(StringComparer.Ordinal);
            if (previousBindings != null)
            {
                foreach (var previousBinding in previousBindings)
                {
                    if (previousBinding == null)
                    {
                        continue;
                    }

                    previousByKey[previousBinding.BindingKey] = previousBinding;
                }
            }

            var mergedBindings = new List<BagTopicBinding>(inventoryEntries.Count);
            foreach (var inventoryEntry in inventoryEntries)
            {
                var binding = new BagTopicBinding(inventoryEntry);
                if (previousByKey.TryGetValue(binding.BindingKey, out var previousBinding))
                {
                    binding.CopyEditableStateFrom(previousBinding);
                }

                mergedBindings.Add(binding);
            }

            return mergedBindings;
        }

        private void NotifyTopicBindingsChanged()
        {
            OnTopicBindingsChanged?.Invoke(this);
        }
    }
}
