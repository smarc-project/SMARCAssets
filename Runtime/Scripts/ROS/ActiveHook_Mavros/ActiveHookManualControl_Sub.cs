using UnityEngine;
using RosMessageTypes.Mavros;
using ROS.Subscribers;
using Smarc.GenericControllers;

namespace ActiveHook.Mavros
{
    [RequireComponent(typeof(HorizontalControllerBase))]
    [RequireComponent(typeof(AltitudeControllerBase))]
    [RequireComponent(typeof(AttitudeControllerBase))]
    public class ActiveHookManualControl_Sub : Actuator_Sub<ManualControlMsg>
    {
        [Header("Mavros Manual Control")]
        public float forwardSpeed = 1f;
        public float strafeSpeed = 1f;
        public float verticalSpeed = 1f;
        public float yawSpeed = 1f;


        AltitudeControllerBase altCtrl;
        AttitudeControllerBase attCtrl;
        HorizontalControllerBase horizCtrl;

        [Header("Debug")]
        public float ReceivedForward =0f;
        public float ReceivedStrafe =0f;
        public float ReceivedVertical =0f;
        public float ReceivedYaw =0f;
       

        void Awake()
        {
            altCtrl = GetComponent<AltitudeControllerBase>();
            attCtrl = GetComponent<AttitudeControllerBase>();
            horizCtrl = GetComponent<HorizontalControllerBase>();
        }


        protected override void UpdateVehicle(bool reset)
        {
            if (reset)
            {
                horizCtrl.TargetVelocity = Vector3.zero;
                altCtrl.TargetVelocity = 0f;
                attCtrl.TargetYawRate = 0f;
                return;
            }

            ReceivedForward = ROSMsg.x;
            ReceivedStrafe = ROSMsg.y;
            ReceivedVertical = ROSMsg.z;
            ReceivedYaw = ROSMsg.r;

            var forwardValue = ROSMsg.x/1000f * forwardSpeed;
            var strafeValue = ROSMsg.y/1000f * strafeSpeed;
            var verticalValue = ((ROSMsg.z/1000f) - 0.5f) * verticalSpeed * 2f; // Map from [0,1] to [-1,1]
            var yawValue = ROSMsg.r/1000f * yawSpeed;

            horizCtrl.TargetVelocity = new Vector3(strafeValue, 0, forwardValue);
            altCtrl.TargetVelocity = verticalValue;
            attCtrl.TargetYawRate = yawValue;
        }
    }
}