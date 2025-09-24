
using System.Collections.Generic;
using UnityEngine;
using RosMessageTypes.Geometry;
using RosMessageTypes.Tf2;
// All the ENU etc conversion come from this:
using Unity.Robotics.ROSTCPConnector.ROSGeometry; 

using Utils = DefaultNamespace.Utils;
using GlobalReferencePoint = GeoRef.GlobalReferencePoint;
using ROS.Core;


namespace ROS.Subscribers
{

    public class TFtoUnity_Sub: ROSBehaviour
    {
        [Header("ROS TF to Unity Sub")]
        public bool drawLines = true;
        public bool createArrows = true;
        public GameObject arrowsPrefab;


        // easting,northing wrt unity-origin.
        public double[] unity_origin_in_utm;
        GlobalReferencePoint globalRef;
        HashSet<string> initialized_utm_map_frames;


        protected override void StartROS()
        {
            unity_origin_in_utm = new double[2];

            // UTM stuff needs speical handling due to their large numbers
            // and Unity's float representation in transforms.
            transform.position = Vector3.zero;
            globalRef = FindFirstObjectByType<GlobalReferencePoint>();
            if(globalRef == null)
            {
                Debug.LogError($"[{transform.name}] No GlobalReferencePoint found! Disabling.");
                enabled = false;
                return;
            }
            double easting, northing, lat, lon;
            (easting, northing, lat, lon) = globalRef.GetUTMLatLonOfObject(gameObject);
            unity_origin_in_utm[0] = easting;
            unity_origin_in_utm[1] = northing;

            initialized_utm_map_frames = new HashSet<string>();

            rosCon.Subscribe<TFMessageMsg>(topic, UpdateMessage);
        }

        Transform GetOrCreate(string id)
        {
            GameObject go = Utils.FindDeepChildWithName(gameObject, id);
            if(go == null)
            {
                go = new GameObject(id);
                go.transform.SetParent(transform);
                if(createArrows && arrowsPrefab != null)
                {
                    var arrows = Instantiate(arrowsPrefab, go.transform.position, go.transform.rotation);
                    arrows.transform.SetParent(go.transform);
                }
            }
            return go.transform;
        }

        void UpdateMessage(TFMessageMsg tfMsg)
        {
            if(globalRef == null)
            {
                Debug.LogError($"[{transform.name}] No GlobalReferencePoint found! Disabling.");
                enabled = false;
                rosCon.Unsubscribe(topic);
                return;
            }
            
            foreach(TransformStampedMsg tfStamped in tfMsg.transforms)
            {
                string parent_id = tfStamped.header.frame_id;                
                string child_id = tfStamped.child_frame_id;
                TransformMsg ros_tf = tfStamped.transform;

                // skip re-doing utm->map or utm->utm transforms. 
                // they are supposed to be static transforms!
                if(initialized_utm_map_frames.Contains($"{parent_id}-{child_id}")) continue;
                
                Transform parent_tf = GetOrCreate(parent_id);
                Transform child_tf = GetOrCreate(child_id);
                // ros_tf is always relative to parent, so we need the same
                // relative transform structure in unity.
                // However, unity and ros have different coordinate frames.
                // ROS = FLU
                // Uni = RUF
                // in addition, globally-referenced frames like utm
                // should be in ENU.
                // ros-utm = ENU
                // uni-utm = EUN
                // so we gotta map these proper.
                // ALSO ALSO, utm-related stuff tend to have large numerical coordinates
                // which break things when put in floats in unity transforms
                // thus, we must work in relation to unity's origin point.
                
                if(parent_id.Contains("utm"))
                {
                    if(globalRef == null) continue;

                    // ENU -> EUN, globally positioned such that
                    // unity's origin ends up at 0,0
                    // and if there is a hierarchy of utm frames they all end
                    // up in the same location.
                    parent_tf.position = ENU.ConvertToRUF(
                        new Vector3(
                            (float) -unity_origin_in_utm[0],
                            (float) -unity_origin_in_utm[1],
                            0));


                    // similarly, the child of any utm frame is also in easting/northing
                    // so it is positioned globally, but this time with the ros_tf included
                    // notice math done in doubles!
                    child_tf.position = ENU.ConvertToRUF(
                        new Vector3(
                            (float) (ros_tf.translation.x - unity_origin_in_utm[0]),
                            (float) (ros_tf.translation.y - unity_origin_in_utm[1]),
                            0));
                    // notice that we dont parent these together!
                    
                    // further, if the child of the utm is a map
                    // then we want it to also orient FLU->RUF
                    // from map down, everything should be
                    // in FLU and no more utm exceptions
                    if(child_id.Contains("map"))
                    {
                        child_tf.localRotation = ENU.ConvertToRUF(
                            new Quaternion(
                                (float)ros_tf.rotation.x,
                                (float)ros_tf.rotation.y,
                                (float)ros_tf.rotation.z,
                                (float)ros_tf.rotation.w));
                    }

                    // special case child: map_gt
                    // we _know_ this is published by US
                    // and we _know_ that it is at unity-origin.
                    // so over-write any position transforms that might
                    // have been modified during the round-trip
                    if(child_id == "map_gt") child_tf.position = Vector3.zero;

                    // lastly, all these utm-map stuff needs to happen _once_
                    initialized_utm_map_frames.Add($"{parent_id}-{child_id}");
                }
                else
                {
                    // at "map" level downwards, things in ROS are FLU
                    // and with relatively small coordinate numbers
                    // such that unity's floats should not kill them.
                    
                    // ASSUMPTION: all maps are children of utms in ros.
                    // which means this "map" frame object is placed where it
                    // should be in the unity world when the parent was "utm".

                    // then put the child frame under this
                    child_tf.SetParent(parent_tf);
                    // and position it
                    
                    child_tf.SetLocalPositionAndRotation(FLU.ConvertToRUF(
                        new Vector3(
                            (float)ros_tf.translation.x,
                            (float)ros_tf.translation.y,
                            (float)ros_tf.translation.z)), FLU.ConvertToRUF(
                        new Quaternion(
                            (float)ros_tf.rotation.x,
                            (float)ros_tf.rotation.y,
                            (float)ros_tf.rotation.z,
                            (float)ros_tf.rotation.w)));
                }
            }
        }

       
        void RecursiveGizmoLines(Transform parent_tf)
        {
            foreach(Transform child_tf in parent_tf)
            {
                Gizmos.color = new Color(1f, 0f, 1f, 1f);
                Gizmos.DrawLine(parent_tf.position, child_tf.position);

                RecursiveGizmoLines(child_tf);
            }
        }

        void OnDrawGizmos()
        {
            if(drawLines) RecursiveGizmoLines(transform);
        }
    }
}