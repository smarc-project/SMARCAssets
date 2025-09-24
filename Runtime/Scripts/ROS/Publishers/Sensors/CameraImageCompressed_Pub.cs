using UnityEngine;
using RosMessageTypes.Sensor;
using Unity.Robotics.Core; //Clock

using ROS.Core;
using CameraImageSensor = VehicleComponents.Sensors.CameraImage;

namespace ROS.Publishers
{
    [RequireComponent(typeof(CameraImageSensor))]
    class CameraImageCompressed_Pub: ROSSensorPublisher<CompressedImageMsg, CameraImageSensor>
    {
        [Header("Compressed Image")]
        [Tooltip("Jpg compression quality. 1=lowest quality")]
        [Range(1,100)]
        public int quality = 75;

        protected override void InitPublisher()
        {
            ROSMsg.format = "rgb8;jpeg compressed rgb8";
            ROSMsg.header.frame_id = $"{robot_name}/{DataSource.linkName}";
        }

        protected override void UpdateMessage()
        {
            ROSMsg.header.stamp = new TimeStamp(Clock.time);
            ROSMsg.data = ImageConversion.EncodeToJPG(DataSource.image, quality);
        }
    }
}
