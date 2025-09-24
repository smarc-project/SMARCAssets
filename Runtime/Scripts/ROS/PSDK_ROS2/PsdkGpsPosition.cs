using UnityEngine;
using Unity.Robotics.Core;
using RosMessageTypes.Sensor;
using GeoRef;
using ROS.Core;
using Force;


namespace M350.PSDK_ROS2
{
    public class PsdkGpsPosition : ROSPublisher<NavSatFixMsg>
    {
        GlobalReferencePoint globalReferencePoint;
        
        MixedBody body;

        protected override void InitPublisher()
        {
            GetMixedBody(out body);
        }

        protected override void UpdateMessage()
        {
            if (globalReferencePoint == null)
            {
                globalReferencePoint = FindFirstObjectByType<GlobalReferencePoint>();
                if (globalReferencePoint == null)
                {
                    Debug.LogError("No GlobalReferencePoint found in the scene. Please add one to use GPS data.");
                    enabled = false;
                    return;
                }
            }

            var (lat, lon) = globalReferencePoint.GetLatLonFromUnityXZ(body.transform.position.x, body.transform.position.z);
            ROSMsg.latitude = lat;
            ROSMsg.longitude = lon;
            ROSMsg.altitude = body.transform.position.y;
            ROSMsg.header.stamp = new TimeStamp(Clock.Now);
        }
    }
}