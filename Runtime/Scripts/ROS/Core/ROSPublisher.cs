using UnityEngine;
using ROSMessage = Unity.Robotics.ROSTCPConnector.MessageGeneration.Message;
using Unity.Robotics.Core;


namespace ROS.Core
{
    public abstract class ROSPublisher<RosMsgType> : ROSBehaviour
        where RosMsgType: ROSMessage, new()
    {
        [Header("ROS Publisher")]
        public float frequency = 10f;
        
        protected RosMsgType ROSMsg;
        protected string robot_name = "";

        bool registered = false;
        FrequencyTimer timer;


        protected override void StartROS()
        {
            timer = new FrequencyTimer(frequency);
            ROSMsg = new RosMsgType();
            if(!registered)
            {
                rosCon.RegisterPublisher<RosMsgType>(topic);
                registered = true;
            }
            if (GetRobotGO(out var robotGO))
            {
                robot_name = robotGO.name;
            }
            InitPublisher();
        }

        /// <summary>
        /// Override this method to update the ROS message with the sensor data.
        /// This method is called in Update, so that the message can be published at a fixed frequency.
        /// </summary>
        protected abstract void UpdateMessage();

        /// <summary>
        /// Override this method to initialize the ROS message.
        /// This method is called in StartROS which is called in Start, so that the message can be published at a fixed frequency.
        /// </summary>
        protected virtual void InitPublisher(){}

        /// <summary>
        /// Publish the message to ROS.
        /// We do this in FixedUpdate, so that things can be disabled and enabled at runtime.
        /// And not in Update, because usually FixedUpdate is called at a consistent rate and faster than frames.
        /// </summary>
        void FixedUpdate()
        {
            while (timer.NeedsTick(Clock.Now))
            {
                UpdateMessage();
                rosCon.Publish(topic, ROSMsg);
                timer.Tick();
            }
        }

    }

}
