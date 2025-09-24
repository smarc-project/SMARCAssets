using UnityEngine;
using RosMessageTypes.Std;
using Utils = DefaultNamespace.Utils;

using Propeller = VehicleComponents.Actuators.Propeller;
using ROS.Core;

namespace ROS.Publishers
{
    [RequireComponent(typeof(Propeller))]
    public class PropellerFeedback_Pub: ROSSensorPublisher<Float32Msg, Propeller>
    {
        Propeller prop;
        protected override void InitPublisher()
        {
            prop = GetComponent<Propeller>();
            if(prop == null)
            {
                Debug.Log("No propeller found!");
                enabled = false;
                return;
            }
        }

        protected override void UpdateMessage()
        {
            if(prop == null) return;

            ROSMsg.data = (int)prop.rpm;
        }
    }
}
