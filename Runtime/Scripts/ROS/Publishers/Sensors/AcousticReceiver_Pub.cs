
using UnityEngine;

using Unity.Robotics.Core; //Clock

using RosMessageTypes.Smarc; // StringStampedMsg
using TX = VehicleComponents.Acoustics.Transceiver;
using StringStamped = VehicleComponents.Acoustics.StringStamped;
using ROS.Core;

namespace ROS.Publishers
{

    [RequireComponent(typeof(TX))]
    public class AcousticReceiver_Pub : ROSSensorPublisher<StringStampedMsg, TX>
    {
        protected override void UpdateMessage()
        {
            StringStamped dp = DataSource.Read();
            if(dp == null) return;
            ROSMsg.data = dp.Data;
            ROSMsg.time_sent = new TimeStamp(dp.TimeSent);
            ROSMsg.time_received = new TimeStamp(dp.TimeReceived);
        }   
    }
}