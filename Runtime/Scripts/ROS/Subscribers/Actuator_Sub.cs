using UnityEngine;

using Unity.Robotics.Core; //Clock
using Unity.Robotics.ROSTCPConnector;
using ROSMessage = Unity.Robotics.ROSTCPConnector.MessageGeneration.Message;
using ROS.Core;


namespace ROS.Subscribers
{
    public abstract class ActuatorSubscriber : ROSBehaviour
    {
        // Exists solely to be able to acquire all of these in a list, even though
        // they might have different ros msg types. So we can mass enable-disable them.
    }

    public abstract class Actuator_Sub<RosMsgType> : ActuatorSubscriber
    where RosMsgType: ROSMessage, new()
    {
        [Header("ROS Subscriber")]
        [Tooltip("If the subscription doesn't have data at least this frequently, actuator will be reset. Set to < 0 to disable.")]
        public float expectedFrequency = 2f;

        public bool resetting = true;
        public double receivedFrequency;
    
        protected RosMsgType ROSMsg;
        public bool ReceivedFirstMessage = false;
        double lastTime;


        protected override void StartROS()
        {
            ROSMsg = new RosMsgType();
            rosCon = ROSConnection.GetOrCreateInstance();
            rosCon.Subscribe<RosMsgType>(topic, UpdateMessage);
            lastTime = Clock.Now;
        }

        void UpdateMessage(RosMsgType msg)
        {
            ROSMsg = msg;
            ReceivedFirstMessage = true;

            double deltaTime = Clock.Now - lastTime;
            receivedFrequency = 1.0/deltaTime;
            if(receivedFrequency < 0) receivedFrequency=0.0;

            lastTime = Clock.Now;
        }

        protected abstract void UpdateVehicle(bool reset);
        

        void FixedUpdate()
        {
            if(expectedFrequency > 0)
            {
                double deltaTime = Clock.Now - lastTime;
                receivedFrequency = 1.0/deltaTime;
                if(receivedFrequency < 0) receivedFrequency=0.0;
                resetting = receivedFrequency < expectedFrequency;
            }
            else
            {
                resetting = false;
            }
            //only update vehicle if you have reieved the message
            if(ReceivedFirstMessage) UpdateVehicle(resetting);
            
        }
    }
}

