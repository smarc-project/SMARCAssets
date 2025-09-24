using UnityEngine;
using System;
using Unity.Robotics.Core; //Clock
using RosMessageTypes.Smarc;

using SideScanSonar = VehicleComponents.Sensors.Sonar;
using ROS.Core;


namespace ROS.Publishers
{
    [RequireComponent(typeof(SideScanSonar))]
    class SSS_Pub: ROSSensorPublisher<SidescanMsg, SideScanSonar>
    { 
        protected override void InitPublisher()
        {
            ROSMsg.header.frame_id = $"{robot_name}/{DataSource.linkName}";
            ROSMsg.port_channel = new byte[DataSource.NumBucketsPerBeam];
            ROSMsg.starboard_channel = new byte[DataSource.NumBucketsPerBeam];
            ROSMsg.port_channel_angle_high = new byte[DataSource.NumBucketsPerBeam];
            ROSMsg.port_channel_angle_low = new byte[DataSource.NumBucketsPerBeam];
            ROSMsg.starboard_channel_angle_high = new byte[DataSource.NumBucketsPerBeam];
            ROSMsg.starboard_channel_angle_low = new byte[DataSource.NumBucketsPerBeam];

        }

        protected override void UpdateMessage()
        {
            ROSMsg.header.stamp = new TimeStamp(Clock.time);
            var mid = DataSource.NumBucketsPerBeam;
            Array.Copy(DataSource.Buckets, 0, ROSMsg.port_channel, 0, mid);
            Array.Copy(DataSource.Buckets, mid, ROSMsg.starboard_channel, 0, mid);
            Array.Copy(DataSource.BucketsAngleHigh, 0, ROSMsg.port_channel_angle_high, 0, mid);
            Array.Copy(DataSource.BucketsAngleLow, 0, ROSMsg.port_channel_angle_low, 0, mid);
            Array.Copy(DataSource.BucketsAngleHigh, mid, ROSMsg.starboard_channel_angle_high, 0, mid);
            Array.Copy(DataSource.BucketsAngleLow, mid, ROSMsg.starboard_channel_angle_low, 0, mid);
        }
    }
}
