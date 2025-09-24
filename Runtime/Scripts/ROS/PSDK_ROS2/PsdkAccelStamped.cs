using UnityEngine;
using RosMessageTypes.Geometry;
using Unity.Robotics.Core;
using Unity.Robotics.ROSTCPConnector.ROSGeometry;
using VehicleComponents.Sensors;
using ROS.Core;


namespace M350.PSDK_ROS2
{
    [RequireComponent(typeof(IMU))]
    public class PsdkAccelStamped : ROSPublisher<AccelStampedMsg>
    {
        IMU imu;
        protected override void UpdateMessage()
        {
            if(imu == null) imu = GetComponent<IMU>();
            ROSMsg.accel.linear = imu.linearAcceleration.To<ENU>();
            ROSMsg.header.frame_id = "psdk_map_enu";
            ROSMsg.header.stamp = new TimeStamp(Clock.time);
        }



    }
}