using UnityEngine;
using ROS.Core;
using VehicleComponents.Actuators;
using RosMessageTypes.Std;


namespace ROS.Publishers
{
    [RequireComponent(typeof(Hinge))]
    public class HingeFeedback_Pub: ROSSensorPublisher<Float32Msg, Hinge>
    {
        Hinge hinge;
        protected override void InitPublisher()
        {
            hinge = GetComponent<Hinge>();
            if(hinge == null)
            {
                Debug.Log("No Hinge found!");
                enabled = false;
                return;
            }
        }

        protected override void UpdateMessage()
        {
            if(hinge == null) return;
            ROSMsg.data = hinge.angle;
        }
    }
}
