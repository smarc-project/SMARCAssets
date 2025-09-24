using UnityEngine;
using Unity.Robotics.Core; //Clock
using Unity.Robotics.ROSTCPConnector.ROSGeometry;
using RosMessageTypes.Smarc;

using SensorDVL = VehicleComponents.Sensors.DVL;
using ROS.Core;


namespace ROS.Publishers
{
    [RequireComponent(typeof(SensorDVL))]
    class DVL_Pub: ROSSensorPublisher<DVLMsg, SensorDVL>
    { 

        DVLBeamMsg[] beamMsgs;

        protected override void InitPublisher()
        {
            ROSMsg.header.frame_id = $"{robot_name}/{DataSource.linkName}";
            beamMsgs = new DVLBeamMsg[DataSource.numBeams];
            for(int i=0; i < DataSource.numBeams; i++)
            {
                beamMsgs[i] = new DVLBeamMsg();
            }
            ROSMsg.beams = beamMsgs;
        }

        protected override void UpdateMessage()
        {
            ROSMsg.header.stamp = new TimeStamp(Clock.time);
            
            for(int i=0;i < DataSource.numBeams; i++)
            {
                ROSMsg.beams[i].range = DataSource.ranges[i];
            }
            ROSMsg.velocity = DataSource.velocity.To<FLU>();
            ROSMsg.altitude = DataSource.altitude;
        }
    }
}
