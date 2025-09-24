using UnityEngine;
using RosMessageTypes.Std;

using IPercentageActuator = VehicleComponents.Actuators.IPercentageActuator;
using ROS.Core;


namespace ROS.Publishers
{
    [RequireComponent(typeof(IPercentageActuator))]
    public class PercentageFeedback_Pub: ROSSensorPublisher<Float32Msg, IPercentageActuator>
    {
        IPercentageActuator act;
        protected override void InitPublisher()
        {
            act = GetComponent<IPercentageActuator>();
            if(act == null)
            {
                Debug.Log("No IPercentageActuator found!");
                enabled = false;
                return;
            }
        }

        protected override void UpdateMessage()
        {
            if(act == null) return;
            ROSMsg.data = act.GetCurrentValue();
        }
    }
}
