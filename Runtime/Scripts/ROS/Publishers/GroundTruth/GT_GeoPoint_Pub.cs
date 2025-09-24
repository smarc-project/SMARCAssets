using UnityEngine;
using ROS.Core;
using RosMessageTypes.Geographic;
using GeoRef;


namespace ROS.Publishers.GroundTruth
{
    class GT_GeoPoint_Pub : ROSPublisher<GeoPointMsg>
    {
        Transform base_link;
        GlobalReferencePoint globalRef;


        protected override void InitPublisher()
        {
            if (GetBaseLink(out base_link))
            {
                 globalRef = FindFirstObjectByType<GlobalReferencePoint>();
                if (globalRef == null)
                {
                    Debug.LogError("GlobalReferencePoint not found in the scene for GlobalPositionPub");
                    enabled = false;
                    return;
                }
            }
        }

        protected override void UpdateMessage()
        {
            var (lat, lon) = globalRef.GetLatLonFromUnityXZ(base_link.position.x, base_link.position.z);
            ROSMsg.latitude = lat;
            ROSMsg.longitude = lon;
            ROSMsg.altitude = base_link.position.y;
        }
    }
}
