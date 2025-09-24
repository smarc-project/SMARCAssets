using UnityEngine;
using ROS.Core;
using RosMessageTypes.Std;
using VehicleComponents.Sensors;
using System.Collections.Generic;


namespace ROS.Publishers.GroundTruth
{
    class GT_BatteryPercent_Pub : ROSPublisher<Float32Msg>
    {
        
        Transform base_link;
        List<Battery> batteries;


        protected override void InitPublisher()
        {
            if (GetBaseLink(out base_link))
            {
                // Find all Battery components in the children of this GameObject
                batteries = new List<Battery>(base_link.GetComponentsInChildren<Battery>());
            }
        }

        protected override void UpdateMessage()
        {
            if (batteries.Count == 0) ROSMsg.data = 99f; // Some number just so things run
            else
            {
                // Calculate the average battery percentage
                float totalBatteryPercentage = 0f;
                foreach (var battery in batteries)
                {
                    totalBatteryPercentage += battery.currentPercent;
                }
                float averageBatteryPercentage = totalBatteryPercentage / batteries.Count;
                ROSMsg.data = averageBatteryPercentage;
            }
        }
    }
}
