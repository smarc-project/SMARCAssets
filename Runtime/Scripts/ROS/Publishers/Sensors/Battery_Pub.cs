using UnityEngine;
using RosMessageTypes.Sensor;
using Unity.Robotics.Core; //Clock

using SensorBattery = VehicleComponents.Sensors.Battery;
using ROS.Core;

namespace ROS.Publishers
{
    [RequireComponent(typeof(SensorBattery))]
    class Battery_Pub: ROSSensorPublisher<BatteryStateMsg, SensorBattery>
    {
        public bool PercentAsDecimal = false;
        protected override void InitPublisher()
        {
            ROSMsg.header.frame_id = $"{robot_name}/{DataSource.linkName}";
        }

        protected override void UpdateMessage()
        {
            ROSMsg.voltage = DataSource.currentVoltage;
            if (PercentAsDecimal)
                ROSMsg.percentage = DataSource.currentPercent / 100f;
            else
                ROSMsg.percentage = DataSource.currentPercent;
            ROSMsg.header.stamp = new TimeStamp(Clock.time);
        }
    }
}
