using ROS.Core;
using RosMessageTypes.Std;
using UnityEngine;

namespace M350.PSDK_ROS2
{
    public class PsdkHomeAltitude : ROSPublisher<Float32Msg>
    {
        protected override void InitPublisher()
        {
            GetRobotGO(out GameObject body);
            ROSMsg.data = body.transform.position.y;
        }
        
        protected override void UpdateMessage(){}
        
    }
}