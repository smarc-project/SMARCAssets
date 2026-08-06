using UnityEngine;
using Force;

namespace Smarc.GenericControllers
{
    public enum YawControlMode
    {
        CompassHeading,
        YawRate
    }

    public enum TiltMode
    {
        TargetUp,
        ReactToAcceleration
    }


    public class AttitudeControllerBase : MonoBehaviour
    {
        [Header("Robot Body")]
        public ArticulationBody RobotAB;
        public Rigidbody RobotRB;
        protected MixedBody robotBody;


        [Header("Control Modes")]
        public YawControlMode YawControlMode = YawControlMode.CompassHeading;
        public TiltMode TiltMode = TiltMode.TargetUp;

        [Header("Rates")]
        public float TargetYawRate = 5.0f; // Target yaw rate in degrees per second
        public float MaxYawRateDeg = 45f;
        public float TargetCompassHeading = 0f; // Target heading in degrees

        [Tooltip("Desired up direction for the robot, can be used to keep a steady tilt.")]
        public Vector3 TargetUp = Vector3.up;

        [Header("Safety")]
        [Tooltip("Below this up-dot, abandon heading control and just right the drone.")]
        public float UpDotLimit = 0.5f;



        protected void Start()
        {
            robotBody = new MixedBody(RobotAB, RobotRB);
        }

        protected virtual Vector3 GetTargetTiltRate()
        {
            throw new System.NotImplementedException("GetTargetTiltRate() must be implemented in a derived class.");
        }

        protected virtual Vector3 GetTargetYawRate()
        {
            throw new System.NotImplementedException("GetTargetYawRate() must be implemented in a derived class.");
        }

        void FixedUpdate()
        {
            
            // if the robot is too tilted, just upright it first...
            Vector3 tiltRate;
            Vector3 yawRate;
            var upDot = Vector3.Dot(robotBody.transform.up, Vector3.up);
            if (upDot < UpDotLimit)
            {
                Debug.Log($"Robot too tilted for yaw control! upDot: {upDot}");
                TargetUp = Vector3.up;
                tiltRate = GetTargetTiltRate();
                yawRate = Vector3.zero;
            }
            else
            {
                tiltRate = GetTargetTiltRate();
                yawRate = GetTargetYawRate();
            }

            Vector3 targetAngularVelocity = tiltRate + yawRate;
            Vector3 torque = (targetAngularVelocity - robotBody.angularVelocity) / Time.fixedDeltaTime;
            robotBody.AddTorque(torque, ForceMode.Acceleration);
        }


    }

}