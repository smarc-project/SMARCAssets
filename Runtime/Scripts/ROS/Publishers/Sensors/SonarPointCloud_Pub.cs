using UnityEngine;
using RosMessageTypes.Sensor;
using Unity.Robotics.Core; //Clock
using System; //Bit converter

using Sonar = VehicleComponents.Sensors.Sonar;
using ROS.Core;


namespace ROS.Publishers
{
    [RequireComponent(typeof(Sonar))]
    class SonarPointCloud_Pub: ROSSensorPublisher<PointCloud2Msg, Sonar>
    { 
        protected override void InitPublisher()
        {
            // the sonar sensors produce points in Unity world frame, which is what is published as map_gt.
            // if we want to publish points wrt the sensor's own frame, we'd need to transform _every single point_
            // in the sonar sensor itself. which is likely not a good use of cpu time :)
            ROSMsg.header.frame_id = "map_gt"; //$"{robot_name}/{DataSource.linkName}";

            ROSMsg.height = 1; // just one long list of points
            ROSMsg.width = (uint)DataSource.TotalRayCount;
            ROSMsg.is_bigendian = false;
            ROSMsg.is_dense = true;
            // 3x 4bytes (float32 x,y,z) + 1x 1byte (uint8 intensity) = 13bytes
            // Could calc this from the fields field i guess.. but meh.
            ROSMsg.point_step = 13; 
            ROSMsg.row_step = ROSMsg.width * ROSMsg.point_step;
            ROSMsg.data = new byte[ROSMsg.point_step * ROSMsg.width];
            

            ROSMsg.fields = new PointFieldMsg[4];

            ROSMsg.fields[0] = new PointFieldMsg();;
            ROSMsg.fields[0].name = "x";
            ROSMsg.fields[0].offset = 0;
            ROSMsg.fields[0].datatype = PointFieldMsg.FLOAT32;
            ROSMsg.fields[0].count = 1;

            ROSMsg.fields[1] = new PointFieldMsg();;
            ROSMsg.fields[1].name = "y";
            ROSMsg.fields[1].offset = 4;
            ROSMsg.fields[1].datatype = PointFieldMsg.FLOAT32;
            ROSMsg.fields[1].count = 1;

            ROSMsg.fields[2] = new PointFieldMsg();;
            ROSMsg.fields[2].name = "z";
            ROSMsg.fields[2].offset = 8;
            ROSMsg.fields[2].datatype = PointFieldMsg.FLOAT32;
            ROSMsg.fields[2].count = 1;

            ROSMsg.fields[3] = new PointFieldMsg();;
            ROSMsg.fields[3].name = "intensity";
            ROSMsg.fields[3].offset = 12;
            ROSMsg.fields[3].datatype = PointFieldMsg.UINT8;
            ROSMsg.fields[3].count = 1;
        }

        protected override void UpdateMessage()
        {
            ROSMsg.header.stamp = new TimeStamp(Clock.time);
            for(int i=0; i<DataSource.SonarHits.Length; i++)
            {
                byte[] pointByte = DataSource.SonarHits[i].GetBytes();
                Buffer.BlockCopy(pointByte, 0, ROSMsg.data, i*pointByte.Length, pointByte.Length);
            }

        }
    }
}
