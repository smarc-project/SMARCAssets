using UnityEngine;
using Unity.Robotics.Core;
using ROS.Core;
using RosMessageTypes.Std;



public class TestROSBehaviour : ROSBehaviour
{
    Float64Msg ROSMsg;
    [Header("ROS Publisher")]
    public float frequency = 10f;
    float period => 1f/frequency;
    double lastPublished = 0f;
    int i = 0;


    protected override void StartROS()
    {
        rosCon.RegisterPublisher<Float64Msg>(topic);
        ROSMsg = new Float64Msg();
        lastPublished = Clock.Now;
    }


    void FixedUpdate()
    {

        double now = Clock.Now;
        double timeSinceLastPub = now - lastPublished;

        while (timeSinceLastPub >= period)
        {
            ROSMsg.data = ++i;
            rosCon.Publish(topic, ROSMsg);
            lastPublished += period;
            timeSinceLastPub = now - lastPublished;
        }
        

        
    }
}
