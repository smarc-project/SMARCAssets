using UnityEngine;

using RosMessageTypes.Smarc; // StringStampedMsg

using TX = VehicleComponents.Acoustics.Transceiver;
using ROS.Core;

namespace ROS.Subscribers
{

    [RequireComponent(typeof(TX))]
    public class AcousticTransmitter_Sub : ROSBehaviour
    {       
        TX tx;
        
        protected override void StartROS()
        {
            tx = GetComponent<TX>();
            rosCon.Subscribe<StringStampedMsg>(topic, UpdateMessage);
        }

        void UpdateMessage(StringStampedMsg msg)
        {
            if(tx == null)
            {
                Debug.Log($"[{transform.name}] No transceiver found! Disabling.");
                enabled = false;
                rosCon.Unsubscribe(topic);
                return;
            }
            tx.Write(msg.data);
        }

    }
}