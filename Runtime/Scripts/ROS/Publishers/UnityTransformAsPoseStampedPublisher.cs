using DefaultNamespace;
using ROS.Core;
using RosMessageTypes.Geometry;
using Scripts.ROS.Core;
using Unity.Robotics.Core;
using UnityEngine;

namespace Scripts.ROS.Publishers
{
    public class UnityTransformAsPoseStampedPublisher : ROSPublisher<PoseStampedMsg>
    {
        public string parentFrameId = "";
        public Transform parentFrame;

        public Unity.Robotics.ROSTCPConnector.ROSGeometry.CoordinateSpaceSelection targetSpace = Unity.Robotics.ROSTCPConnector.ROSGeometry.CoordinateSpaceSelection.FLU;

        protected override void UpdateMessage()
        {
            ROSMsg.header.stamp = new TimeStamp(Clock.time);
            ROSMsg.header.frame_id = parentFrameId;

            var pos = parentFrame.InverseTransformPoint(transform.position).To(targetSpace);
            var orient = (Quaternion.Inverse(parentFrame.transform.rotation) * transform.rotation).To(targetSpace);

            ROSMsg.pose.position.x = pos.x;
            ROSMsg.pose.position.y = pos.y;
            ROSMsg.pose.position.z = pos.z;

            ROSMsg.pose.orientation.x = orient.x;
            ROSMsg.pose.orientation.y = orient.y;
            ROSMsg.pose.orientation.z = orient.z;
            ROSMsg.pose.orientation.w = orient.w;
        }
    }
}