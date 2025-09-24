using UnityEngine;

using DefaultNamespace; // ResetArticulationBody() extension

using RosMessageTypes.Geometry;
using Unity.Robotics.ROSTCPConnector.ROSGeometry; 
using ROS.Core;
using RosMessageTypes.Std;


namespace ROS.Subscribers
{
    
    public enum TeleportFrame
    {
        Odom,
        Map
    }

    public class Teleporter_Sub : ROSBehaviour
    {
        [Header("Teleporter")]
        [Tooltip("The object to teleport around. Can handle Arti. Bodies too.")]
        public Transform Target;

        ArticulationBody[] ABparts;
        Rigidbody[] RBparts;

        int immovableStage = 2;

        [Tooltip("Choose whether to teleport object in local transform(odom) versus global transform(map)")]
        public TeleportFrame TeleportFrame = TeleportFrame.Odom;


        [Header("Debug")]
        public bool UseDebugInput = false;
        public bool ResetDebugInput = false;
        public Vector3 ROSCoordInput;


        protected override void StartROS()
        {
            ABparts = Target.gameObject.GetComponentsInChildren<ArticulationBody>();
            RBparts = Target.gameObject.GetComponentsInChildren<Rigidbody>();
            ROSCoordInput = ENU.ConvertFromRUF(Target.position);

            rosCon.Subscribe<PoseStampedMsg>(topic, UpdateMessage);
        }


        void UpdateMessage(PoseStampedMsg poseStamped)
        {
            if (Target.TryGetComponent(out ArticulationBody targetAb))
            {
                if (!targetAb.isRoot)
                {
                    Debug.LogWarning($"[{transform.name}] Assigned target object is an Arti. body, but it is not the root. Non-root articulation bodies can not be teleported! Disabling.");
                    enabled = false;
                    rosCon.Unsubscribe(topic);
                    return;
                }
            }

            // check the stamp for any mention of maps or odoms
            var frameId = poseStamped.header.frame_id;
            if (!(frameId.Contains("map") || frameId.Contains("odom")))
            {
                Debug.LogWarning($"[{transform.name}] Received a pose with frame_id {frameId} which is not supported for teleportation. Only ENU frames in odom or map frames are supported. Ignoring!");
                return;
            }

            if (frameId.Contains("map") && TeleportFrame == TeleportFrame.Odom)
            {
                Debug.LogWarning($"[{transform.name}] Received a pose with frame_id {frameId} which is in map(global) frame, but teleporter is set to local space!. Ignoring!");
                return;
            }

            if (frameId.Contains("odom") && TeleportFrame == TeleportFrame.Map)
            {
                Debug.LogWarning($"[{transform.name}] Received a pose with frame_id {frameId} which is in odom frame, but teleporter is set to global(map) space!. Ignoring!");
                return;
            }


            var pose = poseStamped.pose;

            // if its an articulation body, we need to use a specific method
            // otherwise just setting local position/rotation is enough.
            var unityPosi = ENU.ConvertToRUF(
                        new Vector3(
                            (float)pose.position.x,
                            (float)pose.position.y,
                            (float)pose.position.z));

            var unityOri = ENU.ConvertToRUF(
                        new Quaternion(
                            (float)pose.orientation.x,
                            (float)pose.orientation.y,
                            (float)pose.orientation.z,
                            (float)pose.orientation.w));

            if (Target.TryGetComponent(out ArticulationBody _))
            {
                targetAb.immovable = true;
                immovableStage = 0;
                targetAb.TeleportRoot(unityPosi, unityOri);
                targetAb.linearVelocity = Vector3.zero;
                targetAb.angularVelocity = Vector3.zero;
            }
            else
            {
                if (TeleportFrame == TeleportFrame.Map) Target.SetPositionAndRotation(unityPosi, unityOri);
                else Target.SetLocalPositionAndRotation(unityPosi, unityOri);
            }


            foreach (var ab in ABparts)
            {
                ab.linearVelocity = Vector3.zero;
                ab.angularVelocity = Vector3.zero;
                ab.ResetArticulationBody();
            }

            foreach (var rb in RBparts)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }

        void FixedUpdate()
        {
            if (UseDebugInput && immovableStage >= 2)
            {
                UpdateMessage(new PoseStampedMsg
                {
                    pose = new PoseMsg
                    {
                        position = new PointMsg
                        {
                            x = ROSCoordInput.x,
                            y = ROSCoordInput.y,
                            z = ROSCoordInput.z
                        },
                        orientation = new QuaternionMsg
                        {
                            x = 0,
                            y = 0,
                            z = 0,
                            w = 1
                        }
                    },
                    header = new HeaderMsg
                    {
                        frame_id = TeleportFrame == TeleportFrame.Odom ? "odom" : "map"
                    }
                });
                if (ResetDebugInput) UseDebugInput = false;
            }

            switch (immovableStage)
            {
                case 0:
                    immovableStage = 1;
                    break;
                case 1:
                    if (Target.TryGetComponent(out ArticulationBody targetAb))
                    {
                        if (!targetAb.isRoot) return;
                        targetAb.immovable = false;
                    }
                    immovableStage = 2;
                    break;
                default:
                    break;
            }
        }
    }
}
