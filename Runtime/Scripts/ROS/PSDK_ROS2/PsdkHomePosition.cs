using UnityEngine;
using Unity.Robotics.Core;
using RosMessageTypes.Sensor;
using GeoRef;
using ROS.Core;


namespace M350.PSDK_ROS2
{
    public class PsdkHomePosition : ROSPublisher<NavSatFixMsg>
    {
        GlobalReferencePoint globalReferencePoint;

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
            if (GetRobotGO(out GameObject robotGO))
            {
                var (lat, lon) = globalReferencePoint.GetLatLonFromUnityXZ(robotGO.transform.position.x, robotGO.transform.position.z);
                ROSMsg.latitude = lat * Mathf.Deg2Rad;
                ROSMsg.longitude = lon * Mathf.Deg2Rad;
                ROSMsg.altitude = robotGO.transform.position.y;
                Debug.Log($"[PsdkHomePosition] Home position set to: {lat},{lon} from robot position {robotGO.transform.position}");
            }
            else
            {
                Debug.LogError("No robot found for PsdkHomePosition. Please add a robot tag to the robot root.");
                enabled = false;
                return;
            }
        }

        
        protected override void UpdateMessage()
        {
            ROSMsg.header.stamp = new TimeStamp(Clock.time);
        }
    }
}