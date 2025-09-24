using UnityEngine;
using RosMessageTypes.Sensor;
using Unity.Robotics.Core; //Clock

using SensorPressure = VehicleComponents.Sensors.DepthPressure;
using ROS.Core;


namespace ROS.Publishers
{
    [RequireComponent(typeof(SensorPressure))]
    class DepthPressure_Pub: ROSSensorPublisher<FluidPressureMsg, SensorPressure>
    { 
        protected override void InitPublisher()
        {
            ROSMsg.header.frame_id = $"{robot_name}/{DataSource.linkName}";
        }

        protected override void UpdateMessage()
        {
            ROSMsg.header.stamp = new TimeStamp(Clock.time);
            ROSMsg.fluid_pressure = DataSource.pressure;
        }
    }
}
