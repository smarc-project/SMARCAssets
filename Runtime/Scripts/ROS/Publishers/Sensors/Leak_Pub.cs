using UnityEngine;
using RosMessageTypes.Std;

using SensorLeak = VehicleComponents.Sensors.Leak;
using ROS.Core;


namespace ROS.Publishers
{
    [RequireComponent(typeof(SensorLeak))]
    class Leak_Pub: ROSSensorPublisher<BoolMsg, SensorLeak>
    { 
        protected override void UpdateMessage()
        {
            if(DataSource.leaked)
            {
                ROSMsg.data = true;
            }
            else ROSMsg.data = false;
        }
    }
}