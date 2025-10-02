using UnityEngine;
using SensorLoadCell = VehicleComponents.Sensors.LoadCell;
using ROS.Core;
using RosMessageTypes.Std;

namespace ROS.Publishers
{
    [RequireComponent(typeof(SensorLoadCell))]
    class LoadCell_Pub: ROSSensorPublisher<Float32Msg, SensorLoadCell>
    {
        protected override void InitPublisher() { }
        protected override void UpdateMessage()
        {
            ROSMsg.data = DataSource.Weight;
        }
    }
}
