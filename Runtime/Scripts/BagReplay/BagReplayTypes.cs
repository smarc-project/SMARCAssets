using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.Robotics.ROSTCPConnector.MessageGeneration;
using UnityEngine;

namespace BagReplay
{
    public enum BagTopicMappingMode
    {
        Auto,
        None,
        Override
    }

    [Serializable]
    public class BagTopicInventoryEntry
    {
        [SerializeField] private int topicId;
        [SerializeField] private string topicName = string.Empty;
        [SerializeField] private string rosTypeName = string.Empty;
        [SerializeField] private long messageCount;

        public int TopicId => topicId;
        public string TopicName => topicName;
        public string RosTypeName => rosTypeName;
        public long MessageCount => messageCount;

        public BagTopicInventoryEntry(int topicId, string topicName, string rosTypeName, long messageCount)
        {
            this.topicId = topicId;
            this.topicName = topicName ?? string.Empty;
            this.rosTypeName = rosTypeName ?? string.Empty;
            this.messageCount = messageCount;
        }
    }

    [Serializable]
    public class BagTopicBinding
    {
        [SerializeField] private int topicId;
        [SerializeField] private string topicName = string.Empty;
        [SerializeField] private string rosTypeName = string.Empty;
        [SerializeField] private long messageCount;
        [SerializeField] private bool enabled;
        [SerializeField] private BagTopicMappingMode mappingMode = BagTopicMappingMode.Auto;
        [SerializeField] private string overrideRosMessageName = string.Empty;
        [SerializeField] private string resolvedRosMessageName = string.Empty;
        [SerializeField] private string resolvedClrTypeName = string.Empty;
        [SerializeField] private string errorMessage = string.Empty;
        [SerializeField] private long loadedMessageCount;

        public int TopicId => topicId;
        public string TopicName => topicName;
        public string RosTypeName => rosTypeName;
        public long MessageCount => messageCount;
        public string BindingKey => $"{topicName}|{rosTypeName}";
        public bool Enabled
        {
            get => enabled;
            set => enabled = value;
        }

        public BagTopicMappingMode MappingMode
        {
            get => mappingMode;
            set => mappingMode = value;
        }

        public string OverrideRosMessageName
        {
            get => overrideRosMessageName;
            set => overrideRosMessageName = value ?? string.Empty;
        }

        public string ResolvedRosMessageName => resolvedRosMessageName;
        public string ResolvedClrTypeName => resolvedClrTypeName;
        public string ErrorMessage => errorMessage;
        public long LoadedMessageCount => loadedMessageCount;
        public bool HasResolvedMapping => !string.IsNullOrWhiteSpace(resolvedRosMessageName) && string.IsNullOrWhiteSpace(errorMessage);

        public BagTopicBinding(BagTopicInventoryEntry inventoryEntry)
        {
            UpdateMetadata(inventoryEntry);
            enabled = false;
            mappingMode = BagTopicMappingMode.Auto;
        }

        public void UpdateMetadata(BagTopicInventoryEntry inventoryEntry)
        {
            topicId = inventoryEntry.TopicId;
            topicName = inventoryEntry.TopicName;
            rosTypeName = inventoryEntry.RosTypeName;
            messageCount = inventoryEntry.MessageCount;
        }

        public void CopyEditableStateFrom(BagTopicBinding other)
        {
            if (other == null)
            {
                return;
            }

            enabled = other.enabled;
            mappingMode = other.mappingMode;
            overrideRosMessageName = other.overrideRosMessageName;
        }

        public void ClearRuntimeState()
        {
            resolvedRosMessageName = string.Empty;
            resolvedClrTypeName = string.Empty;
            errorMessage = string.Empty;
            loadedMessageCount = 0;
        }

        public void SetRuntimeState(string resolvedRosTypeName, Type resolvedType, long loadedMessages, string runtimeError)
        {
            resolvedRosMessageName = resolvedRosTypeName ?? string.Empty;
            resolvedClrTypeName = resolvedType?.FullName ?? string.Empty;
            loadedMessageCount = loadedMessages;
            errorMessage = runtimeError ?? string.Empty;
        }

        public string GetRequestedRosMessageName()
        {
            return mappingMode switch
            {
                BagTopicMappingMode.None => string.Empty,
                BagTopicMappingMode.Override => overrideRosMessageName,
                _ => rosTypeName
            };
        }
    }

    public sealed class BagTopicPlaybackValue
    {
        public BagTopicBinding Binding { get; }
        public long TimestampNanos { get; }
        public string ResolvedRosMessageName { get; }
        public Message Message { get; }

        public string TopicName => Binding.TopicName;
        public string RosTypeName => Binding.RosTypeName;

        public BagTopicPlaybackValue(BagTopicBinding binding, long timestampNanos, string resolvedRosMessageName, Message message)
        {
            Binding = binding;
            TimestampNanos = timestampNanos;
            ResolvedRosMessageName = resolvedRosMessageName ?? string.Empty;
            Message = message;
        }
    }

    public sealed class BagTopicSnapshot
    {
        private readonly Dictionary<string, BagTopicPlaybackValue> valuesByTopic;

        public static readonly BagTopicSnapshot Empty =
            new BagTopicSnapshot(new Dictionary<string, BagTopicPlaybackValue>(StringComparer.Ordinal));

        public IReadOnlyDictionary<string, BagTopicPlaybackValue> ValuesByTopic => valuesByTopic;
        public IEnumerable<BagTopicPlaybackValue> Values => valuesByTopic.Values;
        public int Count => valuesByTopic.Count;

        public BagTopicSnapshot(Dictionary<string, BagTopicPlaybackValue> valuesByTopic)
        {
            this.valuesByTopic = valuesByTopic ?? new Dictionary<string, BagTopicPlaybackValue>(StringComparer.Ordinal);
        }

        public bool TryGetValue(string topicName, out BagTopicPlaybackValue value)
        {
            return valuesByTopic.TryGetValue(topicName, out value);
        }
    }

    public sealed class RosMessageTypeDescriptor
    {
        public string RosMessageName { get; }
        public Type MessageType { get; }
        public string DisplayName => $"{RosMessageName} ({MessageType.Name})";

        public RosMessageTypeDescriptor(string rosMessageName, Type messageType)
        {
            RosMessageName = rosMessageName;
            MessageType = messageType;
        }
    }

    public static class RosMessageCatalog
    {
        private static readonly object SyncRoot = new object();
        private static List<RosMessageTypeDescriptor> entries;
        private static Dictionary<string, RosMessageTypeDescriptor> entriesByRosName;
        private static Dictionary<string, RosMessageTypeDescriptor> entriesByMatchKey;

        public static IReadOnlyList<RosMessageTypeDescriptor> Entries
        {
            get
            {
                EnsureBuilt();
                return entries;
            }
        }

        public static bool TryGetDescriptor(string rosMessageName, out RosMessageTypeDescriptor descriptor)
        {
            EnsureBuilt();
            if (entriesByRosName.TryGetValue(rosMessageName, out descriptor))
            {
                return true;
            }

            foreach (var matchKey in GetMatchKeys(rosMessageName))
            {
                if (entriesByMatchKey.TryGetValue(matchKey, out descriptor))
                {
                    return true;
                }
            }

            descriptor = null;
            return false;
        }

        public static bool TryResolveRosMessageName(
            string rosMessageName,
            out string resolvedRosMessageName,
            out RosMessageTypeDescriptor descriptor)
        {
            if (TryGetDescriptor(rosMessageName, out descriptor))
            {
                resolvedRosMessageName = descriptor.RosMessageName;
                return true;
            }

            resolvedRosMessageName = string.Empty;
            return false;
        }

        public static bool TryEnsureRegistered(string rosMessageName, out RosMessageTypeDescriptor descriptor)
        {
            if (!TryGetDescriptor(rosMessageName, out descriptor))
            {
                return false;
            }

            var canonicalRosMessageName = descriptor.RosMessageName;
            if (MessageRegistry.GetDeserializeFunction(canonicalRosMessageName) != null)
            {
                return true;
            }

            var registerMethod = descriptor.MessageType.GetMethod("Register", BindingFlags.Public | BindingFlags.Static);
            registerMethod?.Invoke(null, null);
            return MessageRegistry.GetDeserializeFunction(canonicalRosMessageName) != null;
        }

        private static void EnsureBuilt()
        {
            if (entries != null)
            {
                return;
            }

            lock (SyncRoot)
            {
                if (entries != null)
                {
                    return;
                }

                var discoveredEntries = new Dictionary<string, RosMessageTypeDescriptor>(StringComparer.Ordinal);
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    foreach (var type in GetSafeTypes(assembly))
                    {
                        if (!IsGeneratedRosMessageType(type))
                        {
                            continue;
                        }

                        var rosField = type.GetField("k_RosMessageName", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
                        var rosMessageName = rosField != null ? rosField.GetValue(null) as string : null;
                        if (string.IsNullOrWhiteSpace(rosMessageName))
                        {
                            continue;
                        }

                        discoveredEntries[rosMessageName] = new RosMessageTypeDescriptor(rosMessageName, type);
                    }
                }

                entriesByRosName = discoveredEntries;
                entriesByMatchKey = BuildMatchKeyLookup(discoveredEntries.Values);
                entries = discoveredEntries.Values.OrderBy(x => x.RosMessageName, StringComparer.Ordinal).ToList();
            }
        }

        private static Dictionary<string, RosMessageTypeDescriptor> BuildMatchKeyLookup(
            IEnumerable<RosMessageTypeDescriptor> descriptors)
        {
            var matchLookup = new Dictionary<string, RosMessageTypeDescriptor>(StringComparer.Ordinal);
            var ambiguousKeys = new HashSet<string>(StringComparer.Ordinal);

            foreach (var descriptor in descriptors)
            {
                foreach (var matchKey in GetMatchKeys(descriptor.RosMessageName))
                {
                    if (ambiguousKeys.Contains(matchKey))
                    {
                        continue;
                    }

                    if (matchLookup.TryGetValue(matchKey, out var existingDescriptor))
                    {
                        if (!ReferenceEquals(existingDescriptor, descriptor))
                        {
                            matchLookup.Remove(matchKey);
                            ambiguousKeys.Add(matchKey);
                        }

                        continue;
                    }

                    matchLookup[matchKey] = descriptor;
                }
            }

            return matchLookup;
        }

        private static IEnumerable<string> GetMatchKeys(string rosMessageName)
        {
            if (string.IsNullOrWhiteSpace(rosMessageName))
            {
                yield break;
            }

            var segments = rosMessageName.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(segment => !string.IsNullOrWhiteSpace(segment))
                .ToList();

            if (segments.Count == 0)
            {
                yield break;
            }

            var normalizedSegments = new List<string>(segments.Count);
            normalizedSegments.Add(segments[0].Trim().ToLowerInvariant());

            for (var index = 1; index < segments.Count; index++)
            {
                var segment = segments[index].Trim();
                if (IsRosCategorySegment(segment))
                {
                    continue;
                }

                normalizedSegments.Add(segment);
            }

            if (normalizedSegments.Count == 0)
            {
                yield break;
            }

            var lastSegment = normalizedSegments[normalizedSegments.Count - 1];
            foreach (var leafVariant in GetLeafVariants(lastSegment))
            {
                normalizedSegments[normalizedSegments.Count - 1] = leafVariant;
                yield return string.Join("/", normalizedSegments.Select(x => x.ToLowerInvariant()));
            }
        }

        private static IEnumerable<string> GetLeafVariants(string lastSegment)
        {
            if (string.IsNullOrWhiteSpace(lastSegment))
            {
                yield break;
            }

            yield return lastSegment;

            if (lastSegment.EndsWith("Msg", StringComparison.Ordinal))
            {
                yield return lastSegment.Substring(0, lastSegment.Length - 3);
                yield break;
            }

            yield return lastSegment + "Msg";
        }

        private static bool IsRosCategorySegment(string segment)
        {
            return string.Equals(segment, "msg", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(segment, "srv", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(segment, "action", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsGeneratedRosMessageType(Type type)
        {
            return type != null
                   && typeof(Message).IsAssignableFrom(type)
                   && !type.IsAbstract
                   && type.Name.EndsWith("Msg", StringComparison.Ordinal);
        }

        private static IEnumerable<Type> GetSafeTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(x => x != null);
            }
            catch
            {
                return Array.Empty<Type>();
            }
        }
    }
}
