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
        public float rollSpeed = 1f;
        public float pitchSpeed = 1f;


        AltitudeControllerBase altCtrl;
        AttitudeControllerBase attCtrl;
        HorizontalControllerBase horizCtrl;

        [Header("Debug")]
        public float RawForward =0f;
        public float RawLeft =0f;
        public float RawUp =0f;
        public float RawYaw =0f;
        public float RawRoll =0f;
        public float RawPitch =0f;

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
                attCtrl.TargetYawRateDeg = 0f;
                return;
            }

            RawForward = ROSMsg.x;
            RawLeft = ROSMsg.y;
            RawUp = ROSMsg.z;
            RawYaw = ROSMsg.r;
            RawRoll = ROSMsg.t;
            RawPitch = ROSMsg.s;

            var forwardValue = RawForward/1000f * forwardSpeed;
            var strafeValue = RawLeft/1000f * strafeSpeed;
            var verticalValue = ((RawUp/1000f) - 0.5f) * verticalSpeed * 2f; // Map from [0,1] to [-1,1]
            var yawValue = RawYaw/1000f * yawSpeed;
            var rollValue = RawRoll/1000f * rollSpeed;
            var pitchValue = RawPitch/1000f * pitchSpeed;

            horizCtrl.TargetVelocity = new Vector3(strafeValue, 0, forwardValue);
            altCtrl.TargetVelocity = verticalValue;
            attCtrl.TargetYawRateDeg = yawValue;
            attCtrl.TargetRollRateDeg = rollValue;
            attCtrl.TargetPitchRateDeg = pitchValue;

        }
    }
}