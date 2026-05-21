using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.Sqlite;
using RosMessageTypes.Geometry;
using RosMessageTypes.Nav;
using RosMessageTypes.Sam;
using RosMessageTypes.Smarc;
using Unity.Robotics.ROSTCPConnector.MessageGeneration;
using UnityEngine;

namespace BagReplay
{
    public class BagReader
    {
        private const string MissingSqliteMessage =
            "BagReplay could not load the native SQLite dependency 'e_sqlite3'. " +
            "For Unity projects using NuGetForUnity, install the ROSBag playback SQLite set documented in the SMARCAssets README: " +
            "Microsoft.Data.Sqlite 9.0.7 and SQLitePCLRaw.bundle_e_sqlite3 2.1.10.";

        public SortedList<long, PercentStampedMsg> vbs_cmd, vbs_fb;
        public SortedList<long, OdometryMsg> odometry;
        public SortedList<long, PercentStampedMsg> lcg_cmd, lcg_fb;
        public SortedList<long, ThrusterAnglesMsg> angles_cmd;
        public SortedList<long, ThrusterRPMsMsg> rpms_cmd;
        public SortedList<long, PoseStampedMsg> pose;
        public SortedList<long, TwistStampedMsg> twist;

        public double StartNanos;
        public double EndNanos;

        public BagReader(string filePath)
        {
            try
            {
                using var connection = new SqliteConnection($"Data Source={filePath}");
                connection.Open();

                vbs_cmd = ReadMessagesOfType<PercentStampedMsg>(connection, "/sam/core/vbs_cmd");
                vbs_fb = ReadMessagesOfType<PercentStampedMsg>(connection, "/sam/core/vbs_fb");
                lcg_cmd = ReadMessagesOfType<PercentStampedMsg>(connection, "/sam/core/lcg_cmd");
                lcg_fb = ReadMessagesOfType<PercentStampedMsg>(connection, "/sam/core/lcg_fb");

                rpms_cmd = ReadMessagesOfType<ThrusterRPMsMsg>(connection, "/sam/core/thruster_rpms_cmd");
                angles_cmd = ReadMessagesOfType<ThrusterAnglesMsg>(connection, "/sam/core/thrust_vector_cmd");

                odometry = ReadMessagesOfType<OdometryMsg>(connection, "/mocap/sam_mocap/odom");
                pose = ReadMessagesOfType<PoseStampedMsg>(connection, "/mocap/sam_mocap/pose");
                twist = ReadMessagesOfType<TwistStampedMsg>(connection, "/mocap/sam_mocap/velocity");

                StartNanos = vbs_cmd.Keys.Min();
                EndNanos = vbs_cmd.Keys.Max();
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

        public BagData ReadFields(double timeToReadAt)
        {
            if (timeToReadAt >= StartNanos && timeToReadAt <= EndNanos)
            {
                var vbsCmdMsg = vbs_cmd.GetLatestMessage(timeToReadAt);
                var vbsFbMsg = vbs_fb.GetLatestMessage(timeToReadAt);
                var lcgCmdMsg = lcg_cmd.GetLatestMessage(timeToReadAt);
                var lcgFbMsg = lcg_fb.GetLatestMessage(timeToReadAt);
                var rpmMsg = rpms_cmd.GetLatestMessage(timeToReadAt);
                var angleMsg = angles_cmd.GetLatestMessage(timeToReadAt);

                var odometryMsg = odometry.GetLatestMessage(timeToReadAt);
                var poseMsg = pose.GetLatestMessage(timeToReadAt);
                var twistMsg = twist.GetLatestMessage(timeToReadAt);

                BagData bagData = new BagData
                {
                    Vbs_cmd = vbsCmdMsg?.value ?? 0,
                    Vbs_fb = vbsFbMsg?.value ?? 0,
                    Lcg_cmd = lcgCmdMsg?.value ?? 0,
                    Lcg_fb = lcgFbMsg?.value ?? 0,
                    Thruster1RPM = rpmMsg?.thruster_1_rpm ?? 0,
                    Thruster2RPM = rpmMsg?.thruster_2_rpm ?? 0,
                    ThrusterHorizontalRad = angleMsg?.thruster_horizontal_radians ?? 0,
                    ThrusterVerticalRad = angleMsg?.thruster_vertical_radians ?? 0,
                    PositionMocapFRD = new Vector3((float)poseMsg.pose.position.x, (float)poseMsg.pose.position.y, (float)poseMsg.pose.position.z),
                    OrientationMocapFRD = new Quaternion((float)poseMsg.pose.orientation.x, (float)poseMsg.pose.orientation.y, (float)poseMsg.pose.orientation.z, (float)poseMsg.pose.orientation.w),
                    LinearVelocityMocapFRD = new Vector3((float)twistMsg.twist.linear.x, (float)twistMsg.twist.linear.y, (float)twistMsg.twist.linear.z),
                    AngularVelocityMocapFRD = new Vector3((float)twistMsg.twist.angular.x, (float)twistMsg.twist.angular.y, (float)twistMsg.twist.angular.z),
                    LinearVelocityBodyFRD = new Vector3((float)odometryMsg.twist.twist.linear.x, (float)odometryMsg.twist.twist.linear.y, (float)odometryMsg.twist.twist.linear.z),
                    AngularVelocityBodyFRD = new Vector3((float)odometryMsg.twist.twist.angular.x, (float)odometryMsg.twist.twist.angular.y, (float)odometryMsg.twist.twist.angular.z),
                };

                return bagData;
            }

            return null;
        }

        private static SortedList<long, T> ReadMessagesOfType<T>(SqliteConnection connection, string topicName) where T : Message
        {
            var id = GetTopicId(connection, topicName);
            SortedList<long, T> messages = new SortedList<long, T>();

            if (id == null) return messages;

            var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT topic_id, timestamp, data FROM messages WHERE topic_id = $topic_id";
            cmd.Parameters.AddWithValue("$topic_id", id);

            using var reader = cmd.ExecuteReader();
            var method = typeof(T).GetMethod("Deserialize", new[] { typeof(MessageDeserializer) });

            while (reader.Read())
            {
                long timestamp = reader.GetInt64(1);
                byte[] data = (byte[])reader["data"];

                var messageDeserializer = new MessageDeserializer();
                messageDeserializer.InitWithBuffer(data);

                var invoke = method.Invoke(null, new object[] { messageDeserializer });
                messages.Add(timestamp, (T)invoke);
            }

            if (messages.Count == 0) Debug.LogWarning($"Topic '{topicName}' was empty.");

            return messages;
        }

        public static int? GetTopicId(SqliteConnection connection, string topicName)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT id FROM topics WHERE name = $name";
            cmd.Parameters.AddWithValue("$name", topicName);

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return reader.GetInt32(0);
            }

            Debug.LogWarning($"Topic '{topicName}' not found.");
            return null;
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
