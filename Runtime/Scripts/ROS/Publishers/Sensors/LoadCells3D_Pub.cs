using UnityEngine;
using SensorLoadCells3D = VehicleComponents.Sensors.LoadCells3D;
using ROS.Core;
using RosMessageTypes.Std;

namespace ROS.Publishers
{
    [AddComponentMenu("Smarc/ROS/LoadCells3D_Pub")]  
    [RequireComponent(typeof(SensorLoadCells3D))]
    class LoadCells3D_Pub : ROSSensorPublisher<Float32Msg, SensorLoadCells3D>
    {
        private int index = 1;

        protected override void InitPublisher()
        {
            // expect the topic name to end with the index of the load cell, e.g. /load_cell_1, /load_cell_2, /load_cell_3
            char lastChar = topic[topic.Length - 1];
            if (lastChar < '1' || lastChar > '3')
            {
                Debug.LogError($"[LoadCells3D_Pub] Topic name must end with the index of the load cell. Disabling.");
                enabled = false;
                return;
            }
            index = lastChar - '1'; 
        }

        protected override void UpdateMessage()
        {
            ROSMsg.data = DataSource.LoadCellReadings[index];
        }
    }
}
