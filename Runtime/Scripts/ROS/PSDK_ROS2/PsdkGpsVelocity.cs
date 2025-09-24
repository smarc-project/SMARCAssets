using Force;
using ROS.Core;
using RosMessageTypes.Geometry;
using Unity.Robotics.Core;
using Unity.Robotics.ROSTCPConnector.ROSGeometry;


namespace M350.PSDK_ROS2
{
    public class PsdkGpsVelocity : ROSPublisher<TwistStampedMsg>
    {
        MixedBody body;

        protected override void InitPublisher()
        {
            GetMixedBody(out body);
        }
        protected override void UpdateMessage()
        {
            var velocity = body.velocity;
            ROSMsg.twist.linear = velocity.To<ENU>();
            ROSMsg.header.frame_id = "psdk_map_enu";
            ROSMsg.header.stamp = new TimeStamp(Clock.time);
        }
    }
}