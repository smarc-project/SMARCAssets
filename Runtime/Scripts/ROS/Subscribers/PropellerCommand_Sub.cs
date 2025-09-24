using UnityEngine;

using RosMessageTypes.Std;
using Propeller = VehicleComponents.Actuators.Propeller;

namespace ROS.Subscribers
{
    [RequireComponent(typeof(Propeller))]
    public class PropellerCommand_Sub : Actuator_Sub<Float32Msg>
    {        
        Propeller prop;
        
        void Awake()
        {
            prop = GetComponent<Propeller>();
        }

        protected override void UpdateVehicle(bool reset)
        {
            if(prop == null)
            {
                Debug.Log($"[{transform.name}] No propeller found! Disabling.");
                enabled = false;
                rosCon.Unsubscribe(topic);
                return;
            }

            if(reset)
            { 
                prop.SetRpm(0);
                return;
            }

            prop.SetRpm(ROSMsg.data);
        }
    }
}
