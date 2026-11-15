using UnityEngine;
using SensorLoadCells3D = VehicleComponents.Sensors.LoadCells3D;
using ROS.Core;
using RosMessageTypes.Std;
using System.Text.RegularExpressions;

namespace ROS.Publishers
{
    [AddComponentMenu("Smarc/ROS/LoadCells3D_Pub")]  
    [RequireComponent(typeof(SensorLoadCells3D))]
    class LoadCells3D_Pub : ROSSensorPublisher<Float32Msg, SensorLoadCells3D>
    {
        private int index;

        protected override void InitPublisher()
        {
            // expect the topic name to contain "/load_cell_<1-4>_weight", where i is the index of the load cell to publish
            Match match = Regex.Match(topic, @"/load_cell_([1-4])");
            if (!match.Success)
            {
                Debug.LogError($"[LoadCells3D_Pub] Topic name [{topic}] is not in the expected format. Expected format: /load_cell_<1-4>_weight.");
                enabled = false;
                return;
            }
            // match.Groups[0]...whole match, Groups[1]...The first group in parenthesis
            index = match.Groups[1].Value[0] - '1'; // char algebra to convert to index
        }

        protected override void UpdateMessage()
        {
            if (index >= DataSource.LoadCellReadings.Length) return;
            ROSMsg.data = DataSource.LoadCellReadings[index];
        }
    }
}
