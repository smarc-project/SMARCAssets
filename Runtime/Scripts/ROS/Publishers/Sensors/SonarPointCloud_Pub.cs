using UnityEngine;
using RosMessageTypes.Sensor;
using Unity.Robotics.Core; //Clock
using System; //Bit converter

using Sonar = VehicleComponents.Sensors.Sonar;
using ROS.Core;


namespace ROS.Publishers
{
    [AddComponentMenu("Smarc/ROS/SonarPointCloud_Pub")]
    [RequireComponent(typeof(Sonar))]
    class SonarPointCloud_Pub: ROSSensorPublisher<PointCloud2Msg, Sonar>
    {
        int PointCount => DataSource.SonarHits != null
            ? DataSource.SonarHits.Length
            : DataSource.TotalRayCount;

        protected override void InitPublisher()
        {
            // the sonar sensors produce points in Unity world frame, which is what is published as unity_origin.
            // if we want to publish points wrt the sensor's own frame, we'd need to transform _every single point_
            // in the sonar sensor itself. which is likely not a good use of cpu time :)
            ROSMsg.header.frame_id = "unity_origin"; //$"{robot_name}/{DataSource.linkName}";

            ROSMsg.height = 1; // just one long list of points
            ROSMsg.is_bigendian = false;
            ROSMsg.is_dense = false; // Missing or rejected returns have NaN coordinates.
            // 3x 4bytes (float32 x,y,z) + 1x 1byte (uint8 intensity) = 13bytes
            // Could calc this from the fields field i guess.. but meh.
            ROSMsg.point_step = 13;
            SyncPointCloudBuffer(PointCount);

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

        void SyncPointCloudBuffer(int pointCount)
        {
            ROSMsg.width = (uint)pointCount;
            ROSMsg.row_step = ROSMsg.width * ROSMsg.point_step;
            int byteLength = (int)ROSMsg.row_step;
            if (ROSMsg.data == null || ROSMsg.data.Length != byteLength)
                ROSMsg.data = new byte[byteLength];
        }

        protected override void UpdateMessage()
        {
            ROSMsg.header.stamp = new TimeStamp(Clock.time);
            SyncPointCloudBuffer(PointCount);
            for (int i = 0; i < PointCount; i++)
            {
                byte[] pointByte = DataSource.SonarHits[i].GetBytes();
                Buffer.BlockCopy(pointByte, 0, ROSMsg.data, i * pointByte.Length, pointByte.Length);
            }
        }
    }
}
