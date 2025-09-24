using Force;
using ROS.Core;
using RosMessageTypes.Std;

namespace M350.PSDK_ROS2
{
    public class PsdkHomeAltitude : ROSPublisher<Float32Msg>
    {

        float initialAlt;

        MixedBody body;

        protected override void InitPublisher()
        {
            GetMixedBody(out body);
            initialAlt = (float) body.transform.position.y;
        }
        
        protected override void UpdateMessage()
        {
            ROSMsg.data = initialAlt;
        }
        
    }
}