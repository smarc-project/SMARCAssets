using UnityEngine;
using ROS.Core;
using RosMessageTypes.Std;


namespace ROS.Publishers.GroundTruth
{
    class GT_Heading_Pub : ROSPublisher<Float32Msg>
    {
        Transform base_link;
        protected override void InitPublisher()
        {
            GetBaseLink(out base_link);
        }
            

        protected override void UpdateMessage()
        {
            var angle = Vector3.SignedAngle(Vector3.forward, base_link.forward, Vector3.up);
            ROSMsg.data = angle;
        }
    }
}
