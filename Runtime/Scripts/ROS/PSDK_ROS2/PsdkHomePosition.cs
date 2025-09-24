using UnityEngine;
using Unity.Robotics.Core;
using RosMessageTypes.Sensor;
using GeoRef;
using ROS.Core;
using Force;


namespace M350.PSDK_ROS2
{
    public class PsdkHomePosition : ROSPublisher<NavSatFixMsg>
    {
        GlobalReferencePoint globalReferencePoint;
        Vector3 initialPos;

        MixedBody body;

        protected override void InitPublisher()
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
            GetMixedBody(out body);
            var (lat, lon) = globalReferencePoint.GetLatLonFromUnityXZ(body.transform.position.x, body.transform.position.z);
            initialPos.x = (float)lat;
            initialPos.y = (float)lon;
            initialPos.z = (float)body.transform.position.y;
        }

        
        protected override void UpdateMessage()
        {
            ROSMsg.latitude = initialPos.x * Mathf.Deg2Rad;;
            ROSMsg.longitude = initialPos.y * Mathf.Deg2Rad;;
            ROSMsg.altitude = initialPos.z;
            ROSMsg.header.stamp = new TimeStamp(Clock.time);
        }
    }
}