using UnityEngine;
using RosMessageTypes.Sensor;

using ROS.Core;
using Unity.Robotics.Core;
using Smarc.GenericControllers;


namespace M350.PSDK_ROS2
{
    // Feeds the same PSDK Joy setpoint that PsdkJoySubscriber sends to DJIController
    // into the Sysid* controllers instead.
    [AddComponentMenu("Smarc/PSDK_ROS/SysidJoySubscriber")]
    public class SysidJoySubscriber : ROSBehaviour
    {
        public float joy_timeout = 0.5f;
        public float time_since_joy;

        bool registered = false;
        SysidHorizontalController horizCtrl;
        SysidAltitudeController altCtrl;

        JoyMsg joy;

        void Awake()
        {
            // ROSBehaviour.OnEnable() disables this component if topic is empty, and it
            // runs before StartROS(), so the default has to be set here (Awake happens
            // before OnEnable). An explicit value set in the Inspector is left untouched.
            if (string.IsNullOrEmpty(topic)) topic = "wrapper/psdk_ros2/flight_control_setpoint_FLUvelocity_yawrate";
        }

        protected override void StartROS()
        {
            if (horizCtrl == null) horizCtrl = GetComponentInParent<SysidHorizontalController>();
            if (altCtrl == null) altCtrl = GetComponentInParent<SysidAltitudeController>();

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
            if (horizCtrl == null) horizCtrl = GetComponentInParent<SysidHorizontalController>();
            if (altCtrl == null) altCtrl = GetComponentInParent<SysidAltitudeController>();

            time_since_joy = (float)Clock.time - joy.header.stamp.sec - joy.header.stamp.nanosec / Mathf.Pow(10f, 9f);
            if (time_since_joy < joy_timeout && joy.axes.Length >= 3)
            {
                if (horizCtrl != null) horizCtrl.TargetVelocity = new Vector2(joy.axes[0], joy.axes[1]);
                if (altCtrl != null) altCtrl.TargetVelocity = joy.axes[2];
            }
            else
            {
                if (horizCtrl != null) horizCtrl.TargetVelocity = Vector2.zero;
                if (altCtrl != null) altCtrl.TargetVelocity = 0f;
                joy = null;
            }
        }

    }
}
