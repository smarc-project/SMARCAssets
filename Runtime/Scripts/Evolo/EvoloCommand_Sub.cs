
using UnityEngine;

using RosMessageTypes.Std;

namespace ROS.Subscribers
{
    public class EvoloCommand_sub : Actuator_Sub<Float32Msg>
    {        
        public float  value;
        
        void Awake()
        {
            //
        }

        protected override void UpdateVehicle(bool reset)
        {
            if(reset)
            {
                value = 0;
                return;
            }
            value = ROSMsg.data;
        }
    }
}

