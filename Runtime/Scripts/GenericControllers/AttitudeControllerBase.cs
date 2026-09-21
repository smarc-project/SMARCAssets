using UnityEngine;
using Force;
using VehicleComponents.Actuators;

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
        ReactToAcceleration,
        RollPitchRate
    }


    public class AttitudeControllerBase : MonoBehaviour, IGenericTwistActuator
    {
        [Header("Robot Body")]
        public ArticulationBody RobotAB;
        public Rigidbody RobotRB;
        protected MixedBody robotBody;


        [Header("Control Modes")]
        public YawControlMode YawControlMode = YawControlMode.CompassHeading;
        public TiltMode TiltMode = TiltMode.TargetUp;

        [Header("Rates")]
        public float TargetYawRateDeg = 0f; // Target yaw rate in degrees per second
        public float MaxYawRateDeg = 45f;
        public float TargetRollRateDeg = 0f; // Target roll rate in degrees per second
        public float MaxRollRateDeg = 45f;
        public float TargetPitchRateDeg = 0f; // Target pitch rate in degrees per second
        public float MaxPitchRateDeg = 45f;

        [Header("Orientation Hold")]
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

        protected virtual Vector3 GetTargetTiltRateDeg()
        {
            throw new System.NotImplementedException("GetTargetTiltRate() must be implemented in a derived class.");
        }

        protected virtual Vector3 GetTargetYawRateDeg()
        {
            throw new System.NotImplementedException("GetTargetYawRate() must be implemented in a derived class.");
        }

        void FixedUpdate()
        {
            
            // if the robot is too tilted, just upright it first...
            // but only if its in a non-rate mode
            Vector3 tiltRate;
            Vector3 yawRate;
            var upDot = Vector3.Dot(robotBody.transform.up, Vector3.up);
            if (upDot < UpDotLimit && TiltMode != TiltMode.RollPitchRate)
            {
                Debug.Log($"Robot too tilted! upDot: {upDot} < UpDotLimit: {UpDotLimit}.");
                TargetUp = Vector3.up;
                tiltRate = GetTargetTiltRateDeg();
                yawRate = Vector3.zero;
            }
            else
            {
                if(TiltMode != TiltMode.RollPitchRate) tiltRate = GetTargetTiltRateDeg();
                else 
                {
                    tiltRate = new Vector3(TargetPitchRateDeg, 0f, TargetRollRateDeg);
                    tiltRate = robotBody.transform.TransformDirection(tiltRate); // Convert from local to world space
                }

                if(YawControlMode != YawControlMode.YawRate) yawRate = GetTargetYawRateDeg();
                else 
                {
                    yawRate = new Vector3(0f, TargetYawRateDeg, 0f);
                    yawRate = robotBody.transform.TransformDirection(yawRate); // Convert from local to world space
                }
            }


            Vector3 targetAngularVelocity = tiltRate + yawRate;
            targetAngularVelocity[0] = Mathf.Clamp(targetAngularVelocity[0], -MaxPitchRateDeg, MaxPitchRateDeg);
            targetAngularVelocity[1] = Mathf.Clamp(targetAngularVelocity[1], -MaxYawRateDeg, MaxYawRateDeg);
            targetAngularVelocity[2] = Mathf.Clamp(targetAngularVelocity[2], -MaxRollRateDeg, MaxRollRateDeg);
            targetAngularVelocity *= Mathf.Deg2Rad;
            Vector3 torque = (targetAngularVelocity - robotBody.angularVelocity) / Time.fixedDeltaTime;
            robotBody.AddTorque(torque, ForceMode.Acceleration);
        }

        public void SetTwist(Vector3 LinearVelocity, Vector3 AngularVelocity)
        {
            if(YawControlMode == YawControlMode.YawRate)
            {
                TargetYawRateDeg = AngularVelocity.y;
            }
            else
            {
                Debug.LogWarning("SetTwist() called, but YawControlMode is not YawRate. Ignoring.");
            }

            if(TiltMode == TiltMode.RollPitchRate)
            {
                TargetRollRateDeg = AngularVelocity.z;
                TargetPitchRateDeg = AngularVelocity.x; 
            }
            else
            {
                Debug.LogWarning("SetTwist() called, but TiltMode is not RollPitchRate. Ignoring.");
            }
        }

        public (Vector3, Vector3) GetResetValue()
        {
            return (Vector3.zero, Vector3.zero);
        }

        public (Vector3, Vector3) GetCurrentValue()
        {
            return (robotBody.localVelocity, robotBody.angularVelocity);
        }

        public bool HasNewData()
        {
            return true;
        }
    }

}