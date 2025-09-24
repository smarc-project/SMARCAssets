using System.Collections.Generic;
using RosMessageTypes.Geometry;
using RosMessageTypes.Std;
using RosMessageTypes.Tf2;
using Unity.Robotics.Core;
using Unity.Robotics.ROSTCPConnector.ROSGeometry;
using UnityEngine;
using ROS.Core;
using Unity.Robotics.UrdfImporter;

namespace ROS.Publishers
{
    public class ROSTransformTreePublisher : ROSPublisher<TFMessageMsg>
    {
        TransformTreeNode BaseLinkTreeNode;

        GameObject BaseLinkGO;
        GameObject OdomLinkGO;

        void OnValidate()
        {
            if (frequency > 1f / Time.fixedDeltaTime)
            {
                Debug.LogWarning($"TF Publisher update frequency set to {frequency}Hz but Unity updates physics at {1f / Time.fixedDeltaTime}Hz. Setting to Unity's fixedDeltaTime!");
                frequency = 1f / Time.fixedDeltaTime;
            }

            if (topic != "/tf")
            {
                Debug.LogWarning($"TF Publisher topic set to {topic} but should be /tf. Setting to /tf!");
                topic = "/tf";
            }

            if (transform.rotation != Quaternion.identity)
            {
                Debug.LogWarning($"[{transform.name}] TF Publisher transform (probably the root robot object: {transform.name}) is not identity, this will cause issues with the TF tree! Resetting to identity.");
                transform.rotation = Quaternion.identity;
            }

        }

        protected override void InitPublisher()
        {
            // we need map(ENU) -> odom(ENU) -> base_link(ENU) -> children(FLU)
            // if this is not a robot, or doesnt have a base_link, we assume _this object_
            // will work as whatever is missing...
            if (!GetRobotGO(out OdomLinkGO))
            {
                OdomLinkGO = transform.gameObject;
            }
            if (GetBaseLink(out var baseLink))
            {
                BaseLinkGO = baseLink.gameObject;
                BaseLinkTreeNode = new TransformTreeNode(BaseLinkGO);
            }
            else
            {
                BaseLinkTreeNode = new TransformTreeNode(transform.gameObject);
            }

            // use top-level parent as the namespace for this tf tree if there is no robot name
            if (robot_name == "")
            {
                Transform topParent = transform;
                while (topParent.parent != null)
                {
                    topParent = topParent.parent;
                }
                robot_name = topParent.name;
            }
        }

        static void PopulateTFList(List<TransformStampedMsg> tfList, TransformTreeNode tfNode)
        {
            // TODO: Some of this could be done once and cached rather than doing from scratch every time
            // Only generate transform messages from the children, because This node will be parented to the global frame
            foreach (var childTf in tfNode.Children)
            {
                tfList.Add(TransformTreeNode.ToTransformStamped(childTf));

                if (!childTf.IsALeafNode)
                {
                    PopulateTFList(tfList, childTf);
                }
            }
        }


        void PopulateGlobalFrames(List<TransformStampedMsg> tfMessageList)
        {
            // we want globally oriented transforms to be the first in the list.
            // map -> odom and odom -> base_link
            // map frame in unity is the unity origin, so we want a 0-transform for that
            // odom frame is cached in StartROS, it is the position of the robot at game start
            // we want the transform from base_link to odom

            var mapToOdomMsg = new TransformMsg
            {
                translation = OdomLinkGO.transform.To<ENU>().translation,
            };
            var mapToOdom = new TransformStampedMsg(
                new HeaderMsg(new TimeStamp(Clock.time), "map_gt"),
                $"{robot_name}/odom",
                mapToOdomMsg);
            tfMessageList.Add(mapToOdom);

            // base_link is the robot's main frame, so we want to publish the transform from odom to base_link
            var rosOdomPos = ENU.ConvertFromRUF(BaseLinkTreeNode.Transform.localPosition);
            var rosOdomOri = ENU.ConvertFromRUF(BaseLinkTreeNode.Transform.localRotation);
            var odomToBaseLinkMsg = new TransformMsg
            {
                translation = new Vector3Msg(
                    rosOdomPos.x,
                    rosOdomPos.y,
                    rosOdomPos.z),
                rotation = new QuaternionMsg(
                    rosOdomOri.x,
                    rosOdomOri.y,
                    rosOdomOri.z,
                    rosOdomOri.w)
            };
            var odomToBaseLink = new TransformStampedMsg(
                new HeaderMsg(new TimeStamp(Clock.time), $"{robot_name}/odom"),
                $"{robot_name}/{BaseLinkTreeNode.name}",
                odomToBaseLinkMsg);
            tfMessageList.Add(odomToBaseLink);
        }

        protected override void UpdateMessage()
        {
            var tfMessageList = new List<TransformStampedMsg>();
            try
            {
                PopulateTFList(tfMessageList, BaseLinkTreeNode);
            }
            catch (MissingReferenceException)
            {
                // If the object tree was modified after the TF Tree was built
                // such as deleting a child object, this will throw an exception
                // So we need to re-build the TF tree and skip the publish.
                Debug.Log($"[{transform.name}] TF Tree was modified, re-building.");
                BaseLinkTreeNode = new TransformTreeNode(BaseLinkGO);
                return;
            }
            foreach (TransformStampedMsg msg in tfMessageList)
            {
                msg.header.frame_id = $"{robot_name}/{msg.header.frame_id}";
                msg.child_frame_id = $"{robot_name}/{msg.child_frame_id}";
            }

            // populate the global frames last, dont wanna prefix those.
            PopulateGlobalFrames(tfMessageList);

            ROSMsg = new TFMessageMsg(tfMessageList.ToArray());
        }

        public void SetBaseLinkName(string name)
        {
            robot_name = name;
        }

    }
}
