using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using Unity.Robotics.ROSTCPConnector.MessageGeneration;

namespace BagReplay
{
    public class BagReader
    {
        private const string MissingSqliteMessage =
            "BagReplay could not load the native SQLite dependency 'e_sqlite3'. " +
            "For Unity projects using NuGetForUnity, install the ROSBag playback SQLite set documented in the SMARCAssets README: " +
            "Microsoft.Data.Sqlite 9.0.7 and SQLitePCLRaw.bundle_e_sqlite3 2.1.10.";

        private readonly string filePath;
        private readonly Dictionary<string, SortedList<long, BagTopicPlaybackValue>> loadedTopicsByName =
            new Dictionary<string, SortedList<long, BagTopicPlaybackValue>>(StringComparer.Ordinal);

        public IReadOnlyList<BagTopicInventoryEntry> TopicInventory { get; }
        public double StartNanos { get; }
        public double EndNanos { get; }

        public BagReader(string filePath)
        {
            this.filePath = filePath;

            try
            {
                using var connection = OpenConnection();
                TopicInventory = ReadTopicInventory(connection);
                (StartNanos, EndNanos) = ReadTimeBounds(connection);
            }
            catch (DllNotFoundException ex)
            {
                throw new InvalidOperationException(MissingSqliteMessage, ex);
            }
            catch (TypeInitializationException ex) when (ContainsDllNotFound(ex))
            {
                throw new InvalidOperationException(MissingSqliteMessage, ex);
            }
        }

        public void LoadBindings(IReadOnlyList<BagTopicBinding> bindings)
        {
            loadedTopicsByName.Clear();

            if (bindings == null)
            {
                return;
            }

            foreach (var binding in bindings)
            {
                binding?.ClearRuntimeState();
            }

            try
            {
                using var connection = OpenConnection();
                foreach (var binding in bindings)
                {
                    LoadBinding(connection, binding);
                }
            }
            catch (DllNotFoundException ex)
            {
                throw new InvalidOperationException(MissingSqliteMessage, ex);
            }
            catch (TypeInitializationException ex) when (ContainsDllNotFound(ex))
            {
                throw new InvalidOperationException(MissingSqliteMessage, ex);
            }
        }

        public BagTopicSnapshot ReadSnapshot(double timeToReadAt)
        {
            if (loadedTopicsByName.Count == 0)
            {
                return BagTopicSnapshot.Empty;
            }

            if (EndNanos > StartNanos)
            {
                timeToReadAt = Math.Min(timeToReadAt, EndNanos);
            }

            var snapshotValues = new Dictionary<string, BagTopicPlaybackValue>(StringComparer.Ordinal);
            foreach (var topicEntry in loadedTopicsByName)
            {
                var playbackValue = topicEntry.Value.GetLatestMessage(timeToReadAt);
                if (playbackValue != null)
                {
                    snapshotValues[topicEntry.Key] = playbackValue;
                }
            }

            return snapshotValues.Count == 0 ? BagTopicSnapshot.Empty : new BagTopicSnapshot(snapshotValues);
        }

        private void LoadBinding(SqliteConnection connection, BagTopicBinding binding)
        {
            if (binding == null || !binding.Enabled || binding.MappingMode == BagTopicMappingMode.None)
            {
                return;
            }

            var resolvedRosMessageName = ResolveRosMessageName(binding, out var descriptor, out var resolutionError);
            if (!string.IsNullOrWhiteSpace(resolutionError))
            {
                binding.SetRuntimeState(resolvedRosMessageName, descriptor?.MessageType, 0, resolutionError);
                return;
            }

            if (!RosMessageCatalog.TryEnsureRegistered(resolvedRosMessageName, out descriptor))
            {
                binding.SetRuntimeState(
                    resolvedRosMessageName,
                    descriptor?.MessageType,
                    0,
                    $"No generated C# message was found for '{resolvedRosMessageName}'.");
                return;
            }

            var deserialize = MessageRegistry.GetDeserializeFunction(resolvedRosMessageName);
            if (deserialize == null)
            {
                binding.SetRuntimeState(
                    resolvedRosMessageName,
                    descriptor.MessageType,
                    0,
                    $"No deserializer was registered for '{resolvedRosMessageName}'.");
                return;
            }

            var timeline = new SortedList<long, BagTopicPlaybackValue>();
            try
            {
                using var command = connection.CreateCommand();
                command.CommandText =
                    "SELECT timestamp, data FROM messages WHERE topic_id = $topic_id ORDER BY timestamp";
                command.Parameters.AddWithValue("$topic_id", binding.TopicId);

                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    var timestamp = reader.GetInt64(0);
                    var data = (byte[])reader["data"];

                    var deserializer = new MessageDeserializer();
                    deserializer.InitWithBuffer(data);

                    var message = deserialize(deserializer);
                    AddTimelineValue(timeline, timestamp, new BagTopicPlaybackValue(binding, timestamp, resolvedRosMessageName, message));
                }
            }
            catch (Exception ex)
            {
                binding.SetRuntimeState(
                    resolvedRosMessageName,
                    descriptor.MessageType,
                    timeline.Count,
                    $"Failed to deserialize '{binding.TopicName}' as '{resolvedRosMessageName}': {ex.Message}");
                return;
            }

            binding.SetRuntimeState(resolvedRosMessageName, descriptor.MessageType, timeline.Count, string.Empty);
            loadedTopicsByName[binding.TopicName] = timeline;
        }

        private SqliteConnection OpenConnection()
        {
            var connection = new SqliteConnection($"Data Source={filePath}");
            connection.Open();
            return connection;
        }

        private static List<BagTopicInventoryEntry> ReadTopicInventory(SqliteConnection connection)
        {
            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT
                    t.id,
                    t.name,
                    t.type,
                    COUNT(m.rowid) AS message_count
                FROM topics t
                LEFT JOIN messages m ON m.topic_id = t.id
                GROUP BY t.id, t.name, t.type
                ORDER BY t.name";

            var inventory = new List<BagTopicInventoryEntry>();
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                inventory.Add(new BagTopicInventoryEntry(
                    reader.GetInt32(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetInt64(3)));
            }

            return inventory;
        }

        private static (double startNanos, double endNanos) ReadTimeBounds(SqliteConnection connection)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT MIN(timestamp), MAX(timestamp) FROM messages";

            using var reader = command.ExecuteReader();
            if (!reader.Read() || reader.IsDBNull(0) || reader.IsDBNull(1))
            {
                return (0d, 0d);
            }

            return (reader.GetInt64(0), reader.GetInt64(1));
        }

        private static string ResolveRosMessageName(
            BagTopicBinding binding,
            out RosMessageTypeDescriptor descriptor,
            out string errorMessage)
        {
            descriptor = null;
            errorMessage = string.Empty;

            switch (binding.MappingMode)
            {
                case BagTopicMappingMode.None:
                    return string.Empty;
                case BagTopicMappingMode.Override:
                    if (string.IsNullOrWhiteSpace(binding.OverrideRosMessageName))
                    {
                        errorMessage = "Select an override C# ROS message type.";
                        return string.Empty;
                    }

                    RosMessageCatalog.TryGetDescriptor(binding.OverrideRosMessageName, out descriptor);
                    if (descriptor == null)
                    {
                        errorMessage = $"Override message '{binding.OverrideRosMessageName}' is not available in this project.";
                    }

                    return binding.OverrideRosMessageName;
                case BagTopicMappingMode.Auto:
                default:
                    if (!RosMessageCatalog.TryResolveRosMessageName(binding.RosTypeName, out var resolvedRosMessageName, out descriptor))
                    {
                        errorMessage = $"No generated C# ROS message matches '{binding.RosTypeName}'.";
                        return binding.RosTypeName;
                    }

                    return resolvedRosMessageName;
            }
        }

        private static void AddTimelineValue(
            SortedList<long, BagTopicPlaybackValue> timeline,
            long timestamp,
            BagTopicPlaybackValue playbackValue)
        {
            while (timeline.ContainsKey(timestamp))
            {
                timestamp += 1;
            }

            timeline.Add(timestamp, playbackValue);
        }

        private static bool ContainsDllNotFound(Exception exception)
        {
            while (exception != null)
            {
                if (exception is DllNotFoundException)
                {
                    return true;
                }

                exception = exception.InnerException;
            }

            return false;
        }
    }
}
