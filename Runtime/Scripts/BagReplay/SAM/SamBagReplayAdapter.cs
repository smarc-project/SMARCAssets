using System;
using System.Collections.Generic;
using RosMessageTypes.Geometry;
using RosMessageTypes.Nav;
using RosMessageTypes.Sam;
using RosMessageTypes.Smarc;
using Unity.Robotics.ROSTCPConnector.MessageGeneration;
using UnityEngine;
using UnityEngine.Serialization;

namespace Scripts.BagReplay.SAM
{
    public class SamBagReplayAdapter : MonoBehaviour
    {
        public const string VbsCmdTopic = "/sam/core/vbs_cmd";
        public const string VbsFeedbackTopic = "/sam/core/vbs_fb";
        public const string LcgCmdTopic = "/sam/core/lcg_cmd";
        public const string LcgFeedbackTopic = "/sam/core/lcg_fb";
        public const string ThrusterRpmsTopic = "/sam/core/thruster_rpms_cmd";
        public const string ThrusterAnglesTopic = "/sam/core/thrust_vector_cmd";
        public const string OdometryTopic = "/mocap/sam_mocap/odom";
        public const string PoseTopic = "/mocap/sam_mocap/pose";
        public const string TwistTopic = "/mocap/sam_mocap/velocity";

        private static readonly SamTopicDefinition[] RequiredTopics =
        {
            new SamTopicDefinition(VbsCmdTopic, PercentStampedMsg.k_RosMessageName),
            new SamTopicDefinition(VbsFeedbackTopic, PercentStampedMsg.k_RosMessageName),
            new SamTopicDefinition(LcgCmdTopic, PercentStampedMsg.k_RosMessageName),
            new SamTopicDefinition(LcgFeedbackTopic, PercentStampedMsg.k_RosMessageName),
            new SamTopicDefinition(ThrusterRpmsTopic, ThrusterRPMsMsg.k_RosMessageName),
            new SamTopicDefinition(ThrusterAnglesTopic, ThrusterAnglesMsg.k_RosMessageName),
            new SamTopicDefinition(OdometryTopic, OdometryMsg.k_RosMessageName),
            new SamTopicDefinition(PoseTopic, PoseStampedMsg.k_RosMessageName),
            new SamTopicDefinition(TwistTopic, TwistStampedMsg.k_RosMessageName)
        };

        [FormerlySerializedAs("replay")] public BagReplay bagReplay;

        private BagTopicSnapshot cachedCurrentSnapshot;
        private BagTopicSnapshot cachedNextSnapshot;
        private BagTopicSnapshot cachedPreviousSnapshot;
        private SamBagReplayData cachedCurrentSamData = new SamBagReplayData();
        private SamBagReplayData cachedNextSamData = new SamBagReplayData();
        private SamBagReplayData cachedPreviousSamData = new SamBagReplayData();

        public SamBagReplayData CurrentSamData => ProjectCachedSnapshot(
            bagReplay != null ? bagReplay.CurrentTopicSnapshot : null,
            ref cachedCurrentSnapshot,
            ref cachedCurrentSamData);

        public SamBagReplayData NextSamData => ProjectCachedSnapshot(
            bagReplay != null ? bagReplay.NextTopicSnapshot : null,
            ref cachedNextSnapshot,
            ref cachedNextSamData);

        public SamBagReplayData PreviousSamData => ProjectCachedSnapshot(
            bagReplay != null ? bagReplay.PreviousTopicSnapshot : null,
            ref cachedPreviousSnapshot,
            ref cachedPreviousSamData);

        private void Awake()
        {
            bagReplay = GetComponent<BagReplay>();
            bagReplay.OnReplayRestart += HandleReplayRestart;
            bagReplay.OnReplayDone += HandleReplayDone;
        }

        private void Start()
        {
            ApplyRequiredBindingsToReplay(restartPlayback: true);
        }

        private void OnDestroy()
        {
            bagReplay.OnReplayRestart -= HandleReplayRestart;
            bagReplay.OnReplayDone -= HandleReplayDone;
        }

        [ContextMenu("Apply SAM Topic Bindings")]
        public void ApplyRequiredBindingsToReplay()
        {
            ApplyRequiredBindingsToReplay(restartPlayback: false);
        }

        public void ApplyRequiredBindingsToReplay(bool restartPlayback)
        {
            bagReplay = GetComponent<BagReplay>();
            if (bagReplay == null || !bagReplay.HasLoadedBag)
            {
                return;
            }

            var changed = ConfigureReplayBindings(bagReplay);
            if (changed || restartPlayback)
            {
                bagReplay.RefreshTopicConfiguration(restartPlayback);
            }

            ClearCaches();
        }

        public static bool ConfigureReplayBindings(global::Scripts.BagReplay.BagReplay replay)
        {
            if (replay == null)
            {
                return false;
            }

            var changed = false;
            foreach (var topic in RequiredTopics)
            {
                if (!replay.TryGetBinding(topic.TopicName, topic.RosTypeName, out var binding))
                {
                    continue;
                }

                if (!binding.Enabled)
                {
                    binding.Enabled = true;
                    changed = true;
                }

                if (binding.MappingMode != BagTopicMappingMode.Auto)
                {
                    binding.MappingMode = BagTopicMappingMode.Auto;
                    changed = true;
                }

                if (!string.IsNullOrWhiteSpace(binding.OverrideRosMessageName))
                {
                    binding.OverrideRosMessageName = string.Empty;
                    changed = true;
                }
            }

            return changed;
        }

        public static List<BagTopicBinding> CreateDefaultBindings(IReadOnlyList<BagTopicInventoryEntry> inventoryEntries)
        {
            var bindings = new List<BagTopicBinding>();
            if (inventoryEntries == null)
            {
                return bindings;
            }

            foreach (var inventoryEntry in inventoryEntries)
            {
                if (!IsRequiredTopic(inventoryEntry.TopicName, inventoryEntry.RosTypeName))
                {
                    continue;
                }

                bindings.Add(new BagTopicBinding(inventoryEntry)
                {
                    Enabled = true,
                    MappingMode = BagTopicMappingMode.Auto,
                    OverrideRosMessageName = string.Empty
                });
            }

            return bindings;
        }

        public static SamBagReplayData ProjectSnapshot(BagTopicSnapshot snapshot)
        {
            var samData = new SamBagReplayData();
            if (snapshot == null)
            {
                return samData;
            }

            if (TryGetMessage(snapshot, VbsCmdTopic, out PercentStampedMsg percentStampedMsg))
            {
                samData.VbsCommand = percentStampedMsg.value;
            }

            if (TryGetMessage(snapshot, VbsFeedbackTopic, out percentStampedMsg))
            {
                samData.VbsFeedback = percentStampedMsg.value;
            }

            if (TryGetMessage(snapshot, LcgCmdTopic, out percentStampedMsg))
            {
                samData.LcgCommand = percentStampedMsg.value;
            }

            if (TryGetMessage(snapshot, LcgFeedbackTopic, out percentStampedMsg))
            {
                samData.LcgFeedback = percentStampedMsg.value;
            }

            if (TryGetMessage(snapshot, ThrusterRpmsTopic, out ThrusterRPMsMsg thrusterRpmsMsg))
            {
                samData.Thruster1Rpm = thrusterRpmsMsg.thruster_1_rpm;
                samData.Thruster2Rpm = thrusterRpmsMsg.thruster_2_rpm;
            }

            if (TryGetMessage(snapshot, ThrusterAnglesTopic, out ThrusterAnglesMsg thrusterAnglesMsg))
            {
                samData.ThrusterHorizontalRad = thrusterAnglesMsg.thruster_horizontal_radians;
                samData.ThrusterVerticalRad = thrusterAnglesMsg.thruster_vertical_radians;
            }

            if (TryGetMessage(snapshot, PoseTopic, out PoseStampedMsg poseStampedMsg))
            {
                samData.PositionMocapFRD = new Vector3(
                    (float)poseStampedMsg.pose.position.x,
                    (float)poseStampedMsg.pose.position.y,
                    (float)poseStampedMsg.pose.position.z);
                samData.OrientationMocapFRD = new Quaternion(
                    (float)poseStampedMsg.pose.orientation.x,
                    (float)poseStampedMsg.pose.orientation.y,
                    (float)poseStampedMsg.pose.orientation.z,
                    (float)poseStampedMsg.pose.orientation.w);
            }

            if (TryGetMessage(snapshot, TwistTopic, out TwistStampedMsg twistStampedMsg))
            {
                samData.LinearVelocityMocapFRD = new Vector3(
                    (float)twistStampedMsg.twist.linear.x,
                    (float)twistStampedMsg.twist.linear.y,
                    (float)twistStampedMsg.twist.linear.z);
                samData.AngularVelocityMocapFRD = new Vector3(
                    (float)twistStampedMsg.twist.angular.x,
                    (float)twistStampedMsg.twist.angular.y,
                    (float)twistStampedMsg.twist.angular.z);
            }

            if (TryGetMessage(snapshot, OdometryTopic, out OdometryMsg odometryMsg))
            {
                samData.LinearVelocityBodyFRD = new Vector3(
                    (float)odometryMsg.twist.twist.linear.x,
                    (float)odometryMsg.twist.twist.linear.y,
                    (float)odometryMsg.twist.twist.linear.z);
                samData.AngularVelocityBodyFRD = new Vector3(
                    (float)odometryMsg.twist.twist.angular.x,
                    (float)odometryMsg.twist.twist.angular.y,
                    (float)odometryMsg.twist.twist.angular.z);
            }

            return samData;
        }

        private static bool TryGetMessage<T>(BagTopicSnapshot snapshot, string topicName, out T message)
            where T : Message
        {
            message = null;
            if (snapshot == null)
            {
                return false;
            }

            if (!snapshot.TryGetValue(topicName, out var playbackValue) || playbackValue == null)
            {
                return false;
            }

            message = playbackValue.Message as T;
            return message != null;
        }

        private static bool IsRequiredTopic(string topicName, string rosTypeName)
        {
            for (var index = 0; index < RequiredTopics.Length; index++)
            {
                if (string.Equals(RequiredTopics[index].TopicName, topicName, StringComparison.Ordinal) &&
                    RosMessageCatalog.AreEquivalentRosMessageNames(RequiredTopics[index].RosTypeName, rosTypeName))
                {
                    return true;
                }
            }

            return false;
        }

        private SamBagReplayData ProjectCachedSnapshot(
            BagTopicSnapshot snapshot,
            ref BagTopicSnapshot cachedSnapshot,
            ref SamBagReplayData cachedSamData)
        {
            if (ReferenceEquals(snapshot, cachedSnapshot) && cachedSamData != null)
            {
                return cachedSamData;
            }

            cachedSnapshot = snapshot;
            cachedSamData = ProjectSnapshot(snapshot);
            return cachedSamData;
        }

        private void HandleReplayRestart()
        {
            ClearCaches();
        }

        private void HandleReplayDone()
        {
            ClearCaches();
        }

        private void ClearCaches()
        {
            cachedCurrentSnapshot = null;
            cachedNextSnapshot = null;
            cachedPreviousSnapshot = null;
            cachedCurrentSamData = new SamBagReplayData();
            cachedNextSamData = new SamBagReplayData();
            cachedPreviousSamData = new SamBagReplayData();
        }

        private readonly struct SamTopicDefinition
        {
            public string TopicName { get; }
            public string RosTypeName { get; }

            public SamTopicDefinition(string topicName, string rosTypeName)
            {
                TopicName = topicName;
                RosTypeName = rosTypeName;
            }
        }
    }
}