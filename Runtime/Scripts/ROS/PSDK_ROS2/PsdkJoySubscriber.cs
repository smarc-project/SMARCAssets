using UnityEngine;
using RosMessageTypes.Sensor;

using ROS.Core;
using Unity.Robotics.Core;
using dji;


namespace M350.PSDK_ROS2
{
    public class PsdkJoySubscriber : ROSBehaviour
    {
        protected string tf_prefix;
        public float joy_timeout = 0.5f;
        public float time_since_joy;

        bool registered = false;
        DJIController controller = null;

        JoyMsg joy;


        protected override void StartROS()
        {
            if (controller == null)
            {
                controller = GetComponentInParent<DJIController>();
            }

            JoyMsg ROSMsg = new JoyMsg();
            if (!registered)
            {
                rosCon.Subscribe<JoyMsg>(topic, _joy_sub_callback);
                registered = true;
            }
        }

        void _joy_sub_callback(JoyMsg msg)
        {
            joy = msg;
        }

        void FixedUpdate()
        {
            if (joy == null) return;
            if (controller == null)
            {
                controller = GetComponentInParent<DJIController>();
            }
            if (controller != null && joy != null)
            {
                time_since_joy = (float)Clock.time - joy.header.stamp.sec - joy.header.stamp.nanosec / Mathf.Pow(10f, 9f);
                controller.ControllerType = ControllerType.FLU_Velocity;
                if (time_since_joy < joy_timeout && joy.axes.Length >= 3)
                {
                    controller.CommandVelocityFLU.x = joy.axes[0];
                    controller.CommandVelocityFLU.y = joy.axes[1];
                    controller.CommandVelocityFLU.z = joy.axes[2];
                }
                else
                {
                    controller.CommandVelocityFLU.x = 0;
                    controller.CommandVelocityFLU.y = 0;
                    controller.CommandVelocityFLU.z = 0;
                    joy = null;
                }
            }
        }

    }
}