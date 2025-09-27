using Unity.Robotics.Core;
using UnityEngine;
using RosMessageTypes.PsdkInterfaces;
using ROS.Core;
using Unity.Robotics.ROSTCPConnector.ROSGeometry;



namespace M350.PSDK_ROS2
{
    public class PsdkFusedPos : ROSPublisher<PositionFusedMsg>
    {
        Transform base_link;

        protected override void InitPublisher()
        {
            base.InitPublisher();
            if (!GetBaseLink(out base_link))
            {
                Debug.LogError("No base_link found for PsdkFusedPosition. Make sure there is a link with the 'base_link' name under the robot root.");
                enabled = false;
                return;
            }
            ROSMsg.header.frame_id = "psdk_map_enu";
        }

        protected override void UpdateMessage(){
            var ROSPosition = base_link.localPosition.To<ENU>();

            ROSMsg.position.x = ROSPosition.x;
            ROSMsg.position.y = ROSPosition.y;
            ROSMsg.position.z = ROSPosition.z;
            
            ROSMsg.header.stamp = new TimeStamp(Clock.time);
        }
    }
}