using UnityEngine;

using RosMessageTypes.Std;
using IPercentageActuator = VehicleComponents.Actuators.IPercentageActuator;

namespace ROS.Subscribers
{

    [RequireComponent(typeof(IPercentageActuator))]
    public class PercentageCommand_Sub : Actuator_Sub<Float32Msg>
    {
        IPercentageActuator act;

        void Awake()
        {
            act = GetComponent<IPercentageActuator>();
            
        }

        protected override void UpdateVehicle(bool reset)
        {
            if(act == null)
            {
                Debug.Log("No IPercentageActuator found! Disabling.");
                enabled = false;
                rosCon.Unsubscribe(topic);
                return;
            }
            if(reset) act.SetPercentage(act.GetResetValue());
            else act.SetPercentage(ROSMsg.data);
        }
    }
}
