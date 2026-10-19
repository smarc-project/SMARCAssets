using UnityEngine;
using SensorLoadCells3D = VehicleComponents.Sensors.LoadCells3D;
using ROS.Core;
using RosMessageTypes.Std;

namespace ROS.Publishers
{
    [AddComponentMenu("Smarc/ROS/LoadCells3D_Pub")]
    [RequireComponent(typeof(SensorLoadCells3D))]
    class LoadCells3D_Pub : ROSSensorPublisher<Float32MultiArrayMsg, SensorLoadCells3D>
    {
        protected override void InitPublisher()
        {
            ROSMsg.data = new float[3];
        }
        protected override void UpdateMessage()
        {
            ROSMsg.data[0] = DataSource.LoadCellReadings.x;
            ROSMsg.data[1] = DataSource.LoadCellReadings.y;
            ROSMsg.data[2] = DataSource.LoadCellReadings.z;
        }
    }
}
