using UnityEngine;
using ROSMessage = Unity.Robotics.ROSTCPConnector.MessageGeneration.Message;


namespace ROS.Core
{
    [RequireComponent(typeof(IROSPublishable))]
    public abstract class ROSSensorPublisher<RosMsgType, PublishableType> : ROSPublisher<RosMsgType>
        where RosMsgType: ROSMessage, new()
        where PublishableType: IROSPublishable
    {
        // Subclasses should be able to access these
        // to get data from the sensor and put it in
        // ROSMsg as needed.
        protected PublishableType DataSource;

        [Tooltip("If true, we will publish regardless, even if the underlying sensor says no data.")]
        public bool ignoreSensorState = false;

        protected override void StartROS()
        {
            DataSource = GetComponent<PublishableType>();
            base.StartROS();
        }

    }

}
