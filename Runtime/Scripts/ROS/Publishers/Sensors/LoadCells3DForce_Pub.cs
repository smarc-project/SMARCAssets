using UnityEngine;
using SensorLoadCells3D = VehicleComponents.Sensors.LoadCells3D;
using Unity.Robotics.Core; // Clock
using Unity.Robotics.ROSTCPConnector.ROSGeometry; // To<FLU>()
using ROS.Core;
using RosMessageTypes.Geometry;

namespace ROS.Publishers
{
    [AddComponentMenu("Smarc/ROS/LoadCells3DForce_Pub")]
    [RequireComponent(typeof(SensorLoadCells3D))]
    class LoadCells3DForce_Pub : ROSSensorPublisher<WrenchStampedMsg, SensorLoadCells3D>
    {
        protected override void InitPublisher()
        {
            ROSMsg.header.frame_id = $"{robot_name}/{DataSource.linkName}";
        }
        protected override void UpdateMessage()
        {
            ROSMsg.header.stamp = new TimeStamp(Clock.time);
            ROSMsg.wrench.force = DataSource.Force.To<FLU>();
        }
    }
}
