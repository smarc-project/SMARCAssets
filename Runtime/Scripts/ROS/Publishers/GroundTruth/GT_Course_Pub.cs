using UnityEngine;
using ROS.Core;
using RosMessageTypes.Std;
using Force;


namespace ROS.Publishers.GroundTruth
{
    class GT_Course_Pub : ROSPublisher<Float32Msg>
    {
        MixedBody body;


        protected override void InitPublisher()
        {
            if (GetBaseLink(out var base_link))
            {
                var base_link_ab = base_link.GetComponent<ArticulationBody>();
                var base_link_rb = base_link.GetComponent<Rigidbody>();
                body = new MixedBody(base_link_ab, base_link_rb);
                if (!body.isValid)
                {
                    Debug.LogError("Base link doesnt have a valid Rigidbody or ArticulationBody.");
                    enabled = false;
                    return;
                }
            }
        }

        protected override void UpdateMessage()
        {
            var course = Vector3.SignedAngle(Vector3.forward, body.velocity, Vector3.up);
            course = (course + 360) % 360;
            ROSMsg.data = course;
        }
    }
}
